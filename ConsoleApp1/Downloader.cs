using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using YoutubeExplode;
using YoutubeExplode.Common;
using YoutubeExplode.Playlists;
using YoutubeExplode.Videos;
using YoutubeExplode.Videos.Streams;

namespace ConsoleApp1
{
    public class Downloader
    {
        private readonly YoutubeClient _youtube;
        public Downloader()
        {
            this._youtube = new();
        }

        // Recherche sur YouTube les mots-clés passés en argument, puis sort le premier IVideo
        public async Task<IVideo> SearchForFirstSongAsync(string keywords)
        {
            var video = await _youtube.Search.GetVideosAsync(keywords).FirstAsync();
            return video;
        }

        public async Task<Playlist> GetPlaylist(string url)
        {
            if(!Tools.IsAnURL(url))
            {
                throw new FormatException($"'{url}' isn't a correct URL format.");
            }
            return await _youtube.Playlists.GetAsync(url);
        }

        public async Task<IReadOnlyList<IVideo>> GetVideosFromPlaylistAsync(Playlist playlist)
        {
            return await _youtube.Playlists.GetVideosAsync(playlist.Id) ;
        }

        // Pour une IVideo donnée :
        // Récupère et retourne le stream audio de la meilleure qualitée possible
        // d'une vidéo en se servant de son manifest.
        public async Task<Stream> GetStreamFromVideoAsync(IVideo video)
        {
            var streamManifest = await _youtube.Videos.Streams.GetManifestAsync(video.Id);
            var streamInfo = streamManifest.GetAudioOnlyStreams().GetWithHighestBitrate();
            var stream = await _youtube.Videos.Streams.GetAsync(streamInfo);
            return stream;
        }

        // Pour une IVideo donnée :
        // Récupère et retourne le stream audio de la meilleure qualitée possible
        // d'une vidéo en se servant de son manifest.
        // Cette méthode est semblable à GetStreamFromVideoAsync(), mais stocke le stream dans un fichier.
        public async Task<string> DownloadVideoAsync(IVideo video)
        {
            string filename;
            var streamManifest = await _youtube.Videos.Streams.GetManifestAsync(video.Id);
            var streamInfo = streamManifest.GetAudioOnlyStreams().GetWithHighestBitrate();
            await _youtube.Videos.Streams.DownloadAsync(streamInfo, filename = $"audio.{streamInfo.Container}");
            return filename;
        }

        // Retourne un Objet IVideo correspondant à la vidéo dans l'URL donnée.
        public async Task<IVideo> GetVideoFromURLAsync(string url) => await _youtube.Videos.GetAsync(url);
    }
}
