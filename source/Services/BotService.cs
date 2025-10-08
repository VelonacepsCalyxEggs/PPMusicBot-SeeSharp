using Discord;
using Discord.Interactions;
using Discord.WebSocket;
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
        private readonly IServiceProvider _serviceProvider;

        public BotService(
            ILogger<BotService> logger,
            IConfiguration configuration,
            IServiceProvider serviceProvider,
            DiscordSocketClient botClient,
            InteractionService interactionService)
        {
            _configuration = configuration;
            _logger = logger;
            _serviceProvider = serviceProvider;
            _botClient = botClient;
            _interactionService = interactionService;
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
            await _botClient.StopAsync();
        }

        private void SetupEventHandlers()
        {
            _botClient.Log += LogWrapper;
            _botClient.Ready += OnReady;
            _botClient.InteractionCreated += OnInteractionCreated;
            _interactionService.InteractionExecuted += OnInteractionExecuted;
        }

        private async Task OnReady()
        {
            _logger.LogInformation("Bot is ready! Registering commands...");

            try
            {
                await _interactionService.AddModulesAsync(Assembly.GetEntryAssembly(), _serviceProvider);
                _logger.LogInformation($"Loaded {_interactionService.Modules.Count} modules with {_interactionService.SlashCommands.Count} slash commands");

                await RegisterSlashCommands();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to register modules and commands");
            }
        }

        private async Task OnInteractionCreated(SocketInteraction interaction)
        {
            try
            {
                _logger.LogInformation($"Interaction received: {interaction.Type} from {interaction.User.Username}");

                var context = new SocketInteractionContext(_botClient, interaction);
                var result = await _interactionService.ExecuteCommandAsync(context, _serviceProvider);

                if (!result.IsSuccess)
                {
                    _logger.LogError($"Command failed: {result.ErrorReason}");

                    if (!interaction.HasResponded)
                    {
                        await interaction.RespondAsync($"Error: {result.ErrorReason}", ephemeral: true);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to handle interaction");

                if (!interaction.HasResponded)
                {
                    try
                    {
                        await interaction.RespondAsync("An error occurred.", ephemeral: true);
                    }
                    catch (Exception responseEx)
                    {
                        _logger.LogError(responseEx, "Also failed to send error response");
                    }
                }
            }
        }

        private async Task OnInteractionExecuted(ICommandInfo command, IInteractionContext context, IResult result)
        {
            if (!result.IsSuccess)
            {
                _logger.LogError($"Interaction executed but failed: {result.ErrorReason}");

                switch (result.Error)
                {
                    case InteractionCommandError.UnmetPrecondition:
                        if (!context.Interaction.HasResponded)
                            await context.Interaction.RespondAsync($"Unmet precondition: {result.ErrorReason}", ephemeral: true);
                        break;
                    case InteractionCommandError.Exception:
                        if (!context.Interaction.HasResponded)
                            await context.Interaction.RespondAsync($"An exception occurred: {result.ErrorReason}", ephemeral: true);
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

        private async Task RegisterSlashCommands()
        {
            try
            {
                await _interactionService.RegisterCommandsGloballyAsync(true);
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