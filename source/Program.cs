using PPMusicBot.Services;
using PPMusicBot;
using Discord.Interactions;
using Discord.WebSocket;
using Discord;
using Lavalink4NET.Extensions;
using Lavalink4NET.Artwork;
using Lavalink4NET;

var builder = Host.CreateApplicationBuilder(args);

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

//// Register all command modules
//var assembly = Assembly.GetExecutingAssembly();
//var moduleTypes = assembly.GetTypes()
//    .Where(t => t.IsSubclassOf(typeof(InteractionModuleBase)) && !t.IsAbstract);

//foreach (var moduleType in moduleTypes)
//{
//    builder.Services.AddSingleton(moduleType);
//}
var lavalinkSection = builder.Configuration.GetSection("Lavalink");
builder.Services.AddLavalink()
    .ConfigureLavalink(config =>
    {
        config.BaseAddress = new Uri(lavalinkSection["BaseAddress"] ?? "http://localhost:2333");
        config.WebSocketUri = new Uri(lavalinkSection["BaseAddress"] ?? "ws://localhost:2333");
        config.ResumptionOptions = new LavalinkSessionResumptionOptions(timeout: (TimeSpan.Parse(lavalinkSection["HttpClient:Timeout"] ?? "00:00:30")));
        config.HttpClientName = "PPMusicBotC#";
        config.Passphrase = lavalinkSection["Passphrase"] ?? "youshallnotpass";
    });
builder.Services.AddSingleton<ArtworkService>();
builder.Services.AddSingleton<KenobiAPISearchEngineService>();
builder.Services.AddSingleton<BotService>();
builder.Services.AddHostedService<BotWorker>();

var host = builder.Build();
host.Run();