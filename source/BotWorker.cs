using PPMusicBot.Services;

namespace PPMusicBot
{
    public class BotWorker(ILogger<BotWorker> logger, BotService botService) : BackgroundService
    {
        private readonly ILogger<BotWorker> _logger = logger;
        private readonly BotService _botService = botService;

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            if (_logger.IsEnabled(LogLevel.Information))
            {
                _logger.LogInformation("Initialized! Starting Bot Service... ");
            }

            await _botService.StartAsync(stoppingToken);

            while (!stoppingToken.IsCancellationRequested)
            {
                await Task.Delay(1000, stoppingToken);
            }
            _logger.LogInformation($"{nameof(BotWorker)} was shutdown.");
        }
    }
}
