using Discord;
using Discord.Interactions;
using Discord.Rest;
using Discord.WebSocket;
using Lavalink4NET;
using Lavalink4NET.Events.Players;
using Lavalink4NET.Players.Queued;
using Lavalink4NET.Players.Vote;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System.Reflection;

namespace PPMusicBot.Services
{
    public class BotService
    {
        private readonly IConfiguration _configuration;
        private readonly ILogger<BotService> _logger;
        private readonly DiscordSocketClient _botClient;
        private readonly InteractionService _interactionService;
        private readonly IAudioService _audioService;
        private readonly IServiceProvider _serviceProvider;

        public BotService(
            ILogger<BotService> logger,
            IConfiguration configuration,
            IServiceProvider serviceProvider,
            DiscordSocketClient botClient,
            InteractionService interactionService,
            IAudioService audioService)
        {
            _configuration = configuration;
            _logger = logger;
            _serviceProvider = serviceProvider;
            _botClient = botClient;
            _interactionService = interactionService;
            _audioService = audioService;
        }

        public async Task StartAsync(CancellationToken cancellationToken)
        {
            if (_logger.IsEnabled(LogLevel.Information))
            {
                _logger.LogInformation("Bot service is starting!");
            }

            SetupEventHandlers();
            await _botClient.LoginAsync(TokenType.Bot, _configuration["Bot:Token"]);
            await _botClient.StartAsync();
        }

        public async Task StopAsync()
        {
            if (_logger.IsEnabled(LogLevel.Information))
            {
                _logger.LogInformation("Initiating Bot Service graceful shutdown...");
            }
            await _audioService.StopAsync();
            await _botClient.StopAsync();
        }

        private void SetupEventHandlers()
        {
            _botClient.Log += LogWrapper;
            _botClient.Ready += OnReady;
            _botClient.InteractionCreated += OnInteractionCreated;
            _interactionService.InteractionExecuted += OnInteractionExecuted;
            _audioService.Players.PlayerCreated += OnPlayerCreated;
            _audioService.TrackStuck += OnTrackStuck;
            _audioService.TrackException += OnTrackException;
            _audioService.TrackEnded += OnTrackEnded;
        }

        private Task OnTrackEnded(object sender, TrackEndedEventArgs eventArgs)
        {
            _logger.LogInformation("Track Ended.");
            return Task.CompletedTask;
        }

        private async Task OnTrackException(object sender, TrackExceptionEventArgs eventArgs)
        {
            _logger.LogError($"Track Exeption: {eventArgs.Exception.Cause}: {eventArgs.Exception.Message} \n Track: {eventArgs.Track.Title}");
            //VoteLavalinkPlayer pl = (VoteLavalinkPlayer)eventArgs.Player;
            //await pl.SkipAsync();
        }

        private async Task OnTrackStuck(object sender, TrackStuckEventArgs eventArgs)
        {
            _logger.LogWarning("Track stuck.");
            VoteLavalinkPlayer pl = (VoteLavalinkPlayer)eventArgs.Player;
            await pl.SkipAsync();
        }

        private async Task OnReady()
        {
            _logger.LogInformation("Bot is ready!");

            await _audioService.StartAsync();

            try
            {
                await _interactionService.AddModulesAsync(Assembly.GetEntryAssembly(), _serviceProvider);
                _logger.LogInformation($"Loaded {_interactionService.Modules.Count} modules with {_interactionService.SlashCommands.Count} slash commands");

                await RegisterSlashCommands();

                await _botClient.SetStatusAsync(UserStatus.Online);
                await _botClient.SetCustomStatusAsync(">See sharp >looks inside >blind.");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to register modules and commands");
            }
        }

        private async Task OnInteractionCreated(SocketInteraction interaction)
        {
            _logger.LogInformation($"Interaction received: {interaction.Type} from {interaction.User.Username}");

            var context = new SocketInteractionContext(_botClient, interaction);
            await _interactionService.ExecuteCommandAsync(context, _serviceProvider);
        }

        private async Task OnInteractionExecuted(ICommandInfo command, IInteractionContext context, IResult result)
        {
            if (!result.IsSuccess)
            {
                _logger.LogError($"Interaction executed but failed: {result.ErrorReason} {result.Error}");

                switch (result.Error)
                {
                    case InteractionCommandError.UnmetPrecondition:
                        if (!context.Interaction.HasResponded)
                            await context.Interaction.RespondAsync($"Unmet precondition: {result.ErrorReason}", ephemeral: true);
                        break;
                    case InteractionCommandError.Exception:
                        if (!context.Interaction.HasResponded)
                            await context.Interaction.RespondAsync($"An exception occurred: {result.ErrorReason}", ephemeral: true);
                        else await context.Interaction.FollowupAsync($"An error occurred: {result.ErrorReason}", ephemeral: true);
                        break;
                    default:
                        if (!context.Interaction.HasResponded)
                            await context.Interaction.RespondAsync($"An error occurred: {result.ErrorReason}", ephemeral: true);
                        else await context.Interaction.FollowupAsync($"An error occurred: {result.ErrorReason}", ephemeral: true);
                        break;
                }
            }
            else
            {
                _logger.LogInformation($"Interaction {command.Name} executed successfully");
            }
        }

        private Task OnPlayerCreated(object sender, PlayerCreatedEventArgs eventArgs)
        {
            _logger.LogInformation($"Created a new player for {eventArgs.Player.GuildId}");
            return Task.CompletedTask;
        }

        private async Task RegisterSlashCommands()
        {
            try
            {
                //await _interactionService.RegisterCommandsGloballyAsync(true);
                //var globalCommands = await _botClient.Rest.GetGlobalApplicationCommands();
                //foreach (var command in globalCommands)
                //{
                //    await command.DeleteAsync();
                //}

                foreach (var guild in _botClient.Guilds)
                {
                    _logger.LogInformation($"Registering commands for guild: {guild.Name}");
                     await _interactionService.RegisterCommandsToGuildAsync(guild.Id);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error registering commands");
            }
        }

        private Task LogWrapper(LogMessage msg)
        {
            var logLevel = msg.Severity switch
            {
                LogSeverity.Critical => LogLevel.Critical,
                LogSeverity.Error => LogLevel.Error,
                LogSeverity.Warning => LogLevel.Warning,
                LogSeverity.Info => LogLevel.Information,
                LogSeverity.Verbose => LogLevel.Debug,
                LogSeverity.Debug => LogLevel.Trace,
                _ => LogLevel.Information
            };

            _logger.Log(logLevel, msg.Exception, msg.Message);
            return Task.CompletedTask;
        }
    }
}