using Discord;
using Discord.Interactions;
using Discord.WebSocket;
using Lavalink4NET;
using Lavalink4NET.Events.Players;
using Lavalink4NET.InactivityTracking;
using Lavalink4NET.Players;
using Lavalink4NET.Players.Vote;
using System.Reflection;
using System.Text;

namespace PPMusicBot.Services
{
    public class BotService
    {
        private readonly IConfiguration _configuration;
        private readonly ILogger<BotService> _logger;
        private readonly DiscordSocketClient _botClient;
        private readonly InteractionService _interactionService;
        private readonly IAudioService _audioService;
        private readonly IInactivityTrackingService _inactivityTrackingService;
        private readonly IServiceProvider _serviceProvider;
        private readonly MusicService _musicService;
        private readonly DatabaseService _databaseService;

        public BotService(
            ILogger<BotService> logger,
            IConfiguration configuration,
            IServiceProvider serviceProvider,
            DiscordSocketClient botClient,
            InteractionService interactionService,
            IAudioService audioService,
            IInactivityTrackingService inactivityTrackingService,
            MusicService musicService,
            DatabaseService databaseService)
        {
            _configuration = configuration;
            _logger = logger;
            _serviceProvider = serviceProvider;
            _botClient = botClient;
            _interactionService = interactionService;
            _audioService = audioService;
            _inactivityTrackingService = inactivityTrackingService;
            _musicService = musicService;
            _databaseService = databaseService;
        }

        public async Task StartAsync(CancellationToken cancellationToken)
        {
            if (_logger.IsEnabled(LogLevel.Information))
            {
                _logger.LogInformation("Bot service is starting!");
            }

            SetupEventHandlers();
            await _databaseService.StartAsync(cancellationToken);
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
            _audioService.DiscordClient.VoiceStateUpdated += OnVoiceStateUpdated;
            _audioService.DiscordClient.VoiceServerUpdated += OnVoiceServerUpdated;
            _inactivityTrackingService.PlayerInactive += OnPlayerInactive;
        }

        private Task OnPlayerInactive(object sender, Lavalink4NET.InactivityTracking.Events.PlayerInactiveEventArgs eventArgs)
        {
            return Task.CompletedTask;
        }

        private Task OnVoiceServerUpdated(object sender, Lavalink4NET.Clients.Events.VoiceServerUpdatedEventArgs eventArgs)
        {
            _logger.LogInformation($"Voice Server Updated: {eventArgs.VoiceServer.Endpoint.ToString()}");
            return Task.CompletedTask;
        }

        private async Task OnVoiceStateUpdated(object sender, Lavalink4NET.Clients.Events.VoiceStateUpdatedEventArgs eventArgs)
        {
            _logger.LogInformation($"Voice State Updated: {eventArgs.VoiceState.ToString()}");
            await _databaseService.WriteVoiceChannelData(eventArgs.UserId, eventArgs.OldVoiceState.VoiceChannelId, eventArgs.VoiceState.VoiceChannelId, eventArgs.GuildId, DateTime.Now);
            if (eventArgs.IsCurrentUser) return;
            var guild = _botClient.GetGuild(eventArgs.GuildId);
            if (guild == null) return;
            var player = await _audioService.Players.GetPlayerAsync<LavalinkPlayer>(guild.Id);
            if (player == null) return;
            var botVoiceChannel = guild.GetVoiceChannel(player.VoiceChannelId);
            if (botVoiceChannel == null) return;
            var affectedChannelId = eventArgs.OldVoiceState.VoiceChannelId ?? eventArgs.VoiceState.VoiceChannelId;
            if (!(botVoiceChannel.Id == affectedChannelId)) return;
            if (botVoiceChannel.Users.Count == 1)
            {
                ulong? interactionChannelId = _musicService.GetTextChannelId(guild.Id);
                if (interactionChannelId != null) {
                        var interactionChannel = guild.GetTextChannel((ulong)interactionChannelId);
                    if (interactionChannel != null)
                        await interactionChannel.SendMessageAsync("Everyone left the channel.");
                }
                await player.StopAsync();
                await player.DisconnectAsync();
            }
            return;
        }

        private Task OnTrackEnded(object sender, TrackEndedEventArgs eventArgs)
        {
            _logger.LogInformation("Track Ended.");
            VoteLavalinkPlayer player = (VoteLavalinkPlayer)eventArgs.Player;
            if (player.Queue.Count == 0 && player.CurrentItem == null)
            {
                var guild = _botClient.GetGuild(eventArgs.Player.GuildId);
                if (guild != null)
                {
                    var textChannelId = _musicService.GetTextChannelId(eventArgs.Player.GuildId);
                    if (textChannelId != null)
                    {
                        var textChannel = guild.GetTextChannel((ulong)textChannelId);
                        if (textChannel != null)
                            textChannel.SendMessageAsync("The queue is now empty.");
                    }
                }
            }
            return Task.CompletedTask;
        }

        private  Task OnTrackException(object sender, TrackExceptionEventArgs eventArgs)
        {
            _logger.LogError($"Track Exeption: {eventArgs.Exception.Cause}: {eventArgs.Exception.Message} \n Track: {eventArgs.Track.Title}");
            //VoteLavalinkPlayer pl = (VoteLavalinkPlayer)eventArgs.Player;
            //await pl.SkipAsync()
            return Task.CompletedTask;
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
                await _interactionService.AddModulesAsync(Assembly.GetEntryAssembly(), _serviceProvider);
                _logger.LogInformation($"Loading {_interactionService.Modules.Count} modules with {_interactionService.SlashCommands.Count} slash commands");

                // Remove all existing modules first
                foreach (var guild in _botClient.Guilds)
                {
                    await _interactionService.RemoveModulesFromGuildAsync(guild.Id);
                }

                // Log all loaded commands for debugging
                foreach (var module in _interactionService.Modules)
                {
                    StringBuilder sb = new StringBuilder();
                    sb.AppendLine($"Module: {module.Name}");
                    foreach (var command in module.SlashCommands)
                    {
                        sb.AppendLine($"Slash Command: {command.Name}");
                    }
                    foreach (var command in module.ComponentCommands)
                    {
                        sb.AppendLine($"Component Command: {command.Name}");
                    }
                    _logger.LogInformation(sb.ToString());
                }

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
                LogSeverity.Debug => LogLevel.Debug,
                LogSeverity.Verbose => LogLevel.Trace,
                _ => LogLevel.Information
            };

            _logger.Log(logLevel, msg.Exception, msg.Message);
            return Task.CompletedTask;
        }
    }
}