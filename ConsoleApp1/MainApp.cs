namespace ConsoleApp1
{
    using Discord;
    using Discord.Commands;
    using Discord.WebSocket;
    using System;
    using System.Threading.Tasks;
    using Microsoft.Extensions.DependencyInjection;

    public class MainApp
    {
        private DiscordSocketClient? _client;
        private readonly CommandService _commands;
        private readonly IServiceProvider _services;

        public MainApp()
        {
            _commands = new CommandService();
            _services = ConfigureServices();
        }

        public static void Main(string[] args)
            => new MainApp().MainAsync().GetAwaiter().GetResult();

        public async Task MainAsync()
        {
            var config = new DiscordSocketConfig()
            {
                GatewayIntents = GatewayIntents.AllUnprivileged | GatewayIntents.MessageContent
            };
            _client = new DiscordSocketClient(config);

            await _commands.AddModuleAsync<MusicModule>(_services);
            await Log(new LogMessage(LogSeverity.Info, this.ToString(), $"YoutubeModule ver. {MusicModule.Version} loaded successfully."));
            _client.MessageReceived += HandleCommandAsync;
            _client.Log += Log;

            // Authenticate the bot using the Discord.NET library
            string token = "NDU3NDU0MjgxMTg5MDk3NDcz.GvUFjy.rF89mAed4ThNpsJYBWQ7KfYCOApV0l6tpVol1M";
            await _client.LoginAsync(TokenType.Bot, token);
            await _client.StartAsync();
            await _client.SetGameAsync("YouTube | !y");

            // Keep the bot running
            await Task.Delay(-1);
        }

        private async Task HandleCommandAsync(SocketMessage messageParam)
        {
            Console.WriteLine("Registered : " + messageParam.Content);
            if (!(messageParam is SocketUserMessage message)) return;
            
            int argPos = 0;
            if (!message.HasCharPrefix('!', ref argPos)) return;

            var context = new SocketCommandContext(_client, message);
            var result = await _commands.ExecuteAsync(context, argPos, _services);

            if (!result.IsSuccess && result.Error != CommandError.UnknownCommand)
            {
                Console.WriteLine(result.ErrorReason);
            }
            return;
        }

        public static Task Log(LogMessage msg)
        {
            Console.WriteLine(msg.ToString());
            return Task.CompletedTask;
        }

        private IServiceProvider ConfigureServices()
        {
            return new ServiceCollection()
                .AddSingleton<Downloader>()
                .BuildServiceProvider();
        }
    }
}