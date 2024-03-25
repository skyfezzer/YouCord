using Discord;
using Discord.Audio;
using Discord.Commands;
using System.Collections.Concurrent;
using System.Diagnostics;
using YoutubeExplode.Videos;

namespace ConsoleApp1
{
    public class MusicModule : ModuleBase<SocketCommandContext>
    {
        private Downloader _downloader;
        private static CancellationTokenSource _tokenSource;
        private static ConcurrentQueue<IVideo> _songQueue;
        private static IVideo? _currentSong;
        private static IAudioClient? _audioClient;
        private static IVoiceChannel? _voiceChannel;
        private static bool botIsBusy;

        public const string Version = "1.1";
        public const string QueueClearedMessage = "La queue a été vidée avec succès.";
        public const string UserNotInChannelMessage = "Vous devez être connecté à un salon pour faire ça.";
        public const string BotAndUserNotInSameChannelMessage = "Vous devez être dans le même salon que le bot pour faire ça.";
        public const string SongAddedToQueueMessage = "Ajouté à la queue : {1}";
        public const string SearchingForVideoMessage = "Recherche de `{1}`...";
        public const string PlaylistEmptyDisconnectMessage = "Ma playlist est vide, j'y vais ! A la prochaine !";
        public const string SongPlayingMessage = "Je joue maintenant : {1}";
        public const string NothingToPlayMessage = "La playlist est vide, je n'ai plus de musique à jouer.";
        public const string PrintQueueMessage = "A suivre :\n{1}";
        public const string ArgumentIsNotUrlMessage = "Vous devez passer une URL en argument de cette commande.";
        public const string PlaylistAddedToQueueMessage = "{1} musiques ont été ajouté à la queue.";
        public const string AddingPlaylistMessage = "Ajout de la playlist `{1}` à la queue en cours... (~1s/video)";
        public const string CannotDisconnectNowMessage = "Le bot ne peut pas se déconnecter maintenant.";

        public MusicModule(Downloader downloader)
        {
            this._downloader = downloader;
        }

        [Command("y", RunMode = RunMode.Async)]
        public async Task PrintHelp()
        {
            string help = "**Commandes :**" +
                "\n!yt `https://www.youtube.com/watch?v=XXXXXXXXXXX`" +
                "\n!yt `goofy ass beats`" +
                "\n!ylist `https://www.youtube.com/playlist?list=XXXXXXXXXXXXXXXXXXXXX`\n" +
                "\n!ys # Skip le son actuel." +
                "\n!yq # Affiche la queue en cours." +
                "\n!ystop # Vide la queue, et stop la lecture." +
                "\n!yquit # Déconnecte le bot" +
                "";
            await ReplyAsync(help);
        }
        [Command("yt", RunMode = RunMode.Async)]
        public async Task PlaySongAsync([Remainder, Name("url ou mots")] string args)
        {
            _songQueue ??= new();

            await MainApp.Log(new Discord.LogMessage(Discord.LogSeverity.Info, this.ToString(), $"appel de PlaySongAsync({args})"));
            // Si le sender n'est pas dans un salon audio, renvoie un message d'erreur et arrête.
            if (GetUserChannel() is null)
            {
                await ReplyAsync(UserNotInChannelMessage);
                return;
            }
            // Sinon, on vérifie que le bot n'est pas en train de jouer dans un autre salon que le salon actuel.
            else if (_currentSong is not null)
                if (GetBotChannel().Id != GetUserChannel()?.Id)
                {
                    await ReplyAsync(BotAndUserNotInSameChannelMessage);
                    return;
                }
            // Préconditions vérifiées, à partir d'ici on éxécute.

            // Si l'argument est une URL, on récupère bêtement le stream en provenance
            // de la vidéo contenue dans l'URL.
            IVideo video;
            if (Tools.IsAnURL(args))
            {
                video = await _downloader.GetVideoFromURLAsync(args);
            }
            // Sinon, les arguments sont à interpréter comme des keywords.
            // On fait une recherche des keywords, et on récupère le premier résultat.
            else
            {
                await ReplyAsync(SearchingForVideoMessage.Replace("{1}", args));
                video = await _downloader.SearchForFirstSongAsync(args);
            }

            await AddSongToQueueAsync(video);

        }

        public string GetVideoDetails(IVideo video)
        {
            string formattedDuration = $"{video.Duration:mm\\:ss}";
            return $"{video.Title} [{formattedDuration}] (**{video.Author}**)";
        }

        // Retourne le channel audio actuel du bot.
        public IVoiceChannel GetBotChannel()
        {
            return _voiceChannel;
        }

        [Command("yq", RunMode = RunMode.Async)]
        public async Task PrintQueue()
        {
            _songQueue ??= new();
            int index = 0;
            string result = _songQueue.Aggregate("", (current, video) => current + ($"`#{++index}\t {GetVideoDetails(video)} `\n"));
            var message = await ReplyAsync(PrintQueueMessage.Replace("{1}", result));
        }

        // Selon le contexte, retourne le channel audio du sender.
        public IVoiceChannel? GetUserChannel()
        {
            return (Context.User as IVoiceState).VoiceChannel;
        }



        // Interrompt tout stream, et lance le stream du prochain son dans la queue.
        // Si il n'y a plus de son dans la queue, stop le bot.
        [Command("ys", RunMode = RunMode.Async)]
        public async Task PlayNextSongAsync()
        {
            if (GetUserChannel() is null)
            {
                await ReplyAsync(UserNotInChannelMessage);
                return;
            }
            // Sinon, on vérifie que le bot n'est pas en train de jouer dans un autre salon que le salon actuel.
            else if (_currentSong is not null)
                if (GetBotChannel().Id != GetUserChannel()?.Id)
                {
                    await ReplyAsync(BotAndUserNotInSameChannelMessage);
                    return;
                }
            // Si il y a un son en attente d'être joué
            _songQueue ??= new();
            if (_songQueue.TryDequeue(out var nextSong))
            {
                // Pour éviter les déco/reco intempestives, on va garder en mémoire le client audio.
                // Si un client audio existe déjà, on passe à la suite
                if (_audioClient is null || GetBotChannel() is null)
                {
                    _voiceChannel = GetUserChannel();
                    _audioClient = await _voiceChannel.ConnectAsync();
                    _audioClient.Disconnected += AudioClient_Disconnected;
                }
                else
                {
                    CancelToken();
                }
                // Sinon, on connecte le bot au salon audio du sender et on stocke le client audio.
                // On change le son joué actuellement en mémoire, et on lance son stream.
                // Une fois le son terminé, on passe au suivant de façon récursive.
                _currentSong = nextSong;
                await ReplyAsync(SongPlayingMessage.Replace("{1}", GetVideoDetails(_currentSong)));
                //var songStream = await GetStreamFromVideo(nextSong);
                var songFileName = await _downloader.DownloadVideoAsync(nextSong);
                await StreamSongAsync(songFileName);
                await PlayNextSongAsync();
            }
            // Sinon, il n'y a plus de son dans la queue.
            // On envoie un message d'information, puis on déconnecte le bot.
            else
            {
                _currentSong = null;
                CancelToken();
                await ReplyAsync(NothingToPlayMessage);
                // Implement your logic here to handle the end of the playlist
            }
        }
        [Command("ystop", RunMode = RunMode.Async)]
        public async Task StopEverything()
        {
            IUserMessage message;
            if (GetUserChannel() is null)
            {
                message = await ReplyAsync(UserNotInChannelMessage);
                await Task.Delay(5000);
                await message.DeleteAsync();
                return;
            }
            // Sinon, on vérifie que le bot n'est pas en train de jouer dans un autre salon que le salon actuel.
            else if (_currentSong is not null)
                if (GetBotChannel().Id != GetUserChannel()?.Id)
                {
                    message = await ReplyAsync(BotAndUserNotInSameChannelMessage);
                    await Task.Delay(5000);
                    await message.DeleteAsync();
                    return;
                }
            await ClearQueueAsync();
            CancelToken();
            await PlayNextSongAsync();
        }
        // Ajoute toutes les vidéos contenues dans la playlist à la queue.
        [Command("ylist", RunMode = RunMode.Async)]
        public async Task AddPlaylistToQueue([Remainder, Name("url")] string url)
        {
            if (GetUserChannel() is null)
            {
                await ReplyAsync(UserNotInChannelMessage);
                return;
            }
            // Sinon, on vérifie que le bot n'est pas en train de jouer dans un autre salon que le salon actuel.
            else if (_currentSong is not null)
                if (GetBotChannel().Id != GetUserChannel()?.Id)
                {
                    await ReplyAsync(BotAndUserNotInSameChannelMessage);
                    return;
                }
            if (!Tools.IsAnURL(url))
            {
                await ReplyAsync(ArgumentIsNotUrlMessage);
                return;
            }
            botIsBusy = true;
            var playlist = await _downloader.GetPlaylist(url);
            var videos = await _downloader.GetVideosFromPlaylistAsync(playlist);
            var message = await ReplyAsync(AddingPlaylistMessage.Replace("{1}", playlist.Title));
            foreach (var video in videos)
            {
                await AddSongToQueueAsync(video, true);
            }
            await message.ModifyAsync(msg => msg.Content = PlaylistAddedToQueueMessage.Replace("{1}",videos.Count().ToString()));
            botIsBusy = false;
        }
        private static void CancelToken()
        {
            if (_tokenSource is not null)
            {
                _tokenSource.Cancel();
                _tokenSource.Dispose();
                _tokenSource = null;
            }
        }

        private Task AudioClient_Disconnected(Exception arg)
        {
            _audioClient?.Dispose();
            _audioClient = null;
            _voiceChannel = null;
            return Task.CompletedTask;
        }

        // Par mesure de précaution, on vérifie que le bot est bien connecté à un channel avant d'initier la déconnexion.
        [Command("yquit", RunMode = RunMode.Async)]
        public async Task DisconnectBot()
        {
            if (botIsBusy)
            {
                var message = await ReplyAsync(CannotDisconnectNowMessage);
                await Task.Delay(3000);
                await message.DeleteAsync();
                return;
            }
            if (_voiceChannel != null && _audioClient != null)
            {
                await ClearQueueAsync();
                await ReplyAsync(PlaylistEmptyDisconnectMessage);
                await _audioClient.StopAsync();
                _audioClient = null;
                _voiceChannel = null;
                return;
            }
        }

        // Redirige le stream vers le client audio Discord.
        public async Task StreamSongAsync(Stream inputStream)
        {
            // Si FFmpeg existe, on le kill et on le recréé pour stream un nouveau flux.
            var _ffmpeg = CreateFFmpeg("pipe:0");
            using (var ffinput = _ffmpeg.StandardInput.BaseStream)
            using (var ffoutput = _ffmpeg.StandardOutput.BaseStream)
            using (var discordStream = _audioClient.CreatePCMStream(AudioApplication.Music))
            {
                if (inputStream.CanRead)
                {
                    await inputStream.CopyToAsync(ffinput, _tokenSource.Token);
                }
                else
                {
                    Console.WriteLine("audioStream is not readable");
                }
                // Envoie la sortie de ffmpeg vers
                await ffoutput.CopyToAsync(discordStream, _tokenSource.Token);
                await discordStream.FlushAsync();
            }
        }

        // Redirige le stream d'un fichier vers le client audio Discord.
        public async Task StreamSongAsync(string inputFileName)
        {
            //throw new NotImplementedException();
            // Si FFmpeg existe, on le kill et on le recréé pour stream un nouveau flux.
            var _ffmpeg = CreateFFmpeg(inputFileName);
            using (var ffinput = _ffmpeg.StandardInput.BaseStream)
            using (var ffoutput = _ffmpeg.StandardOutput.BaseStream)
            using (var discordStream = _audioClient.CreatePCMStream(AudioApplication.Music))
            {
                if (ffoutput.CanRead)
                {

                    _tokenSource ??= new();
                    // Envoie la sortie de ffmpeg vers
                    await ffoutput.CopyToAsync(discordStream, _tokenSource.Token);
                    await discordStream.FlushAsync();
                }
                else
                {
                    await ReplyAsync("Le fichier audio téléchargé n'a pas pu être streamé.");
                }
            }
        }

        // Ajoute un IVideo à la queue. Si aucun son n'est en train de jouer, lance le son.
        public async Task AddSongToQueueAsync(IVideo video)
        {
            await AddSongToQueueAsync(video, false);
        }

        // Ajoute un IVideo à la queue. Si aucun son n'est en train de jouer, lance le son.
        public async Task AddSongToQueueAsync(IVideo video, bool silent)
        {
            await Task.Delay(1000);
            _songQueue ??= new();
            _songQueue.Enqueue(video);
            if (!silent)
                await ReplyAsync(SongAddedToQueueMessage.Replace("{1}", GetVideoDetails(video)));
            if (_currentSong == null)
            {
                _ = PlayNextSongAsync();
            }
        }

        // Vide la queue.
        [Command("yc", RunMode = RunMode.Async)]
        public async Task ClearQueueAsync()
        {
            _songQueue.Clear();
            await ReplyAsync(QueueClearedMessage);
        }

        public IVideo GetCurrentSong() => _currentSong;


        private Process CreateFFmpeg(string inputFileName)
        {
#pragma warning disable CS8603 // Possible null reference return.
            // If an error is thrown and the StackTrace points here,
            // it means that FFmpeg is either missing or unreachable.
            var process = Process.Start(new ProcessStartInfo
            {
                FileName = "ffmpeg",
                Arguments = $"-hide_banner -loglevel panic -i {inputFileName} -ac 2 -f s16le -ar 48000 pipe:1 -nostdin",
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardInput = true,
            });
#pragma warning restore CS8603 // Possible null reference return.

            return process;
        }
    }
}
