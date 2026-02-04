using PPMusicBot.Services;

public class BotWorker : BackgroundService
{
    private readonly ILogger<BotWorker> _logger;
    private readonly BotService _botService;
    private readonly IHostApplicationLifetime _applicationLifetime;

    public BotWorker(
        ILogger<BotWorker> logger,
        BotService botService,
        IHostApplicationLifetime applicationLifetime)
    {
        _logger = logger;
        _botService = botService;
        _applicationLifetime = applicationLifetime;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            _logger.LogInformation("Initialized! Starting Bot Service...");

            _applicationLifetime.ApplicationStopping.Register(() =>
            {
                _logger.LogInformation("Application is stopping...");
            });

            await _botService.StartAsync(stoppingToken);

            await Task.Delay(Timeout.Infinite, stoppingToken);
        }
        catch (TaskCanceledException)
        {
            _logger.LogInformation("BotWorker execution cancelled");
        }
        catch
        {
            await StopAsync(stoppingToken);
        }
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("BotWorker stop requested");

        try
        {
            await _botService.StopAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error stopping BotService");
        }
    }
}