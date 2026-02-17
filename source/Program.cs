using Discord;
using Discord.Interactions;
using Discord.WebSocket;
using Lavalink4NET;
using Lavalink4NET.Artwork;
using Lavalink4NET.Extensions;
using Lavalink4NET.InactivityTracking;
using Lavalink4NET.InactivityTracking.Extensions;
using PPMusicBot;
using PPMusicBot.Services;
using Serilog;
using Serilog.Sinks.SystemConsole.Themes;
try
{

    var builder = Host.CreateApplicationBuilder(args);
    bool isProduction = builder.Environment.IsProduction();
    builder.Services.Configure<HostOptions>(options =>
    {
        options.ShutdownTimeout = TimeSpan.FromSeconds(30);
        options.BackgroundServiceExceptionBehavior = BackgroundServiceExceptionBehavior.StopHost;
        options.ServicesStartConcurrently = false;
        options.ServicesStopConcurrently = false;
    });
    var logsPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "PPMusicBot", "logs/ppmusicbot-.txt");
    var loggerConfiguration = new LoggerConfiguration()
        .Enrich.FromLogContext()
        .WriteTo.File(
            path: logsPath,
            rollingInterval: RollingInterval.Day,
            retainedFileCountLimit: 30,
            shared: true)
        .WriteTo.Console(theme: AnsiConsoleTheme.Literate);

    if (isProduction)
    {
        loggerConfiguration.MinimumLevel.Information();
    }
    else
    {
        loggerConfiguration.MinimumLevel.Verbose();
    }

    Log.Logger = loggerConfiguration.CreateLogger();
    builder.Services.AddSerilog();
    Log.Information($"Current Directory: {Directory.GetCurrentDirectory()}");
    Log.Information($"Environment: {Environment.GetEnvironmentVariable("DOTNET_ENVIRONMENT")}");

    builder.Services.AddSingleton<DiscordSocketClient>(provider =>
    {
        var config = new DiscordSocketConfig()
        {
            MessageCacheSize = 32,
            GatewayIntents = GatewayIntents.AllUnprivileged | GatewayIntents.MessageContent,
            LogLevel = LogSeverity.Info
        };
        return new DiscordSocketClient(config);
    });

    builder.Services.AddSingleton<InteractionService>(provider =>
    {
        var client = provider.GetRequiredService<DiscordSocketClient>();
        var interactionConfig = new InteractionServiceConfig()
        {
            LogLevel = LogSeverity.Info,
            UseCompiledLambda = true,
            DefaultRunMode = RunMode.Async
        };
        return new InteractionService(client, interactionConfig);
    });

    var lavalinkSection = builder.Configuration.GetSection("Lavalink");
    builder.Services.AddLavalink()
        .ConfigureLavalink(config =>
        {
            config.BaseAddress = new Uri(lavalinkSection["BaseAddress"] ?? "http://localhost:2333");
            config.WebSocketUri = new Uri(lavalinkSection["WebSocketUri"] ?? "ws://localhost:2333");
            config.ResumptionOptions = new LavalinkSessionResumptionOptions(timeout: (TimeSpan.Parse(lavalinkSection["HttpClient:Timeout"] ?? "00:00:30")));
            config.HttpClientName = "PPMusicBotC#";
            config.Passphrase = lavalinkSection["Passphrase"] ?? "youshallnotpass";
        });
    builder.Services.AddInactivityTracking();
    builder.Services.ConfigureInactivityTracking(config =>
    {
        config.DefaultTimeout = new TimeSpan(0, 30, 0);
        config.InactivityBehavior = PlayerInactivityBehavior.None;
        config.TrackingMode = InactivityTrackingMode.Any;
    });
    builder.Services.AddSingleton<ArtworkService>();
    builder.Services.AddSingleton<MusicService>();
    builder.Services.AddSingleton<KenobiAPISearchEngineService>();
    builder.Services.AddSingleton<DatabaseService>();
    builder.Services.AddSingleton<BotService>();
    builder.Services.AddHostedService<BotWorker>();

    var host = builder.Build();
    host.Run();
}
catch (Exception ex)
{
    Log.Fatal(ex, "Application terminated unexpectedly");
}
finally
{
    Log.CloseAndFlush();
}