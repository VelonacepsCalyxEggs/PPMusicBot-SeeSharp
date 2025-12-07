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
            if (_logger.IsEnabled(LogLevel.Information))
            {
                _logger.LogInformation("Initialized! Starting Bot Service...");
            }

            _applicationLifetime.ApplicationStopping.Register(() =>
            {
                _logger.LogInformation("Application is stopping...");
            });

            await _botService.StartAsync(stoppingToken);

            while (!stoppingToken.IsCancellationRequested)
            {
                await Task.Delay(1000, stoppingToken);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "BotWorker encountered an error");
            throw;
        }
        finally
        {
            _logger.LogInformation($"{nameof(BotWorker)} was shutdown.");
        }
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("BotWorker stop requested");
        await _botService.StopAsync();
        await base.StopAsync(cancellationToken);
    }
}