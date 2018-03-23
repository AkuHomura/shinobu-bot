using Discord;
using Discord.Commands;
using Discord.WebSocket;
using IllyaBot.Common;
using IllyaBot.Databases;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.IO;
using System.Threading.Tasks;
using System.Threading;
using IllyaBot.Services;
using IllyaBot.Handlers;
using Discord.Addons.Interactive;
using System.Linq;

namespace IllyaBot
{
    internal class Program
    {
        private static void Main(string[] args) =>
            new Program().RunAsync().GetAwaiter().GetResult();

        private DiscordSocketClient _client;
        private CommandHandler _handler;
        private GlobalConfig _globalConfig;
        private NuitActive _activeNuit;

        private IServiceProvider _serviceProvider;

        private async Task RunAsync()
        {

            _client = new DiscordSocketClient(new DiscordSocketConfig
            {
#if DEBUG
                LogLevel = LogSeverity.Verbose
#else
                LogLevel = LogSeverity.Error
#endif
            });

            _client.Log += (log) => Task.Run(() => Log.Write(Status.KAY, Source.Client, log.Message));

            GlobalConfig.EnsureExists();
            _globalConfig = GlobalConfig.Load();

            NuitActive.EnsureExists();
            _activeNuit = NuitActive.Load();

            _serviceProvider = ConfigureServices();
            await _client.LoginAsync(TokenType.Bot, _globalConfig.Token);
            await _client.StartAsync();

            _handler = new CommandHandler();
            await _handler.Install(_serviceProvider);

            _client.Ready += Ready;

            await Task.Delay(-1);
        }

        private async Task Ready()
        {
            Log.PrintInfo();
            await Task.CompletedTask;

            await _client.SetGameAsync("@Illya Help");

            var service = _serviceProvider.GetRequiredService<NuitInteractiveService>();
            var nuitService = _serviceProvider.GetRequiredService<NuitService>();
            var nuitActive = _serviceProvider.GetRequiredService<NuitActive>();

            foreach (var guild in _client.Guilds)
            {
                var chan = guild.GetTextChannel(await guild.GetChanIdAsync());
                if(chan!=null)
                {
                    foreach (var message in await chan.GetMessagesAsync(10000).Flatten())
                    {
                        int animeId = nuitService.GetAnimeIdFromEmbed((Embed)message.Embeds.First());
                        await service.SetMessageReactionCallback(message as IUserMessage, nuitActive, nuitService, animeId);
                    }
                }
            }

            
        }


        private IServiceProvider ConfigureServices()
        {
            var command = new CommandService(new CommandServiceConfig { CaseSensitiveCommands = false, ThrowOnError = false });
            

            var services = new ServiceCollection()
                .AddSingleton(_client)
                .AddSingleton(_activeNuit)
                .AddSingleton(command)
                .AddSingleton<InteractiveService>()
                .AddSingleton<NuitInteractiveService>()
                .AddSingleton<NuitService>();
            var provider = new DefaultServiceProviderFactory().CreateServiceProvider(services);
            return provider;
        }
    }
}