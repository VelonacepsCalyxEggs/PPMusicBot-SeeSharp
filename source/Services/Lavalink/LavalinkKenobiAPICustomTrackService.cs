using Lavalink4NET.Rest.Entities.Tracks;
using Lavalink4NET.Tracks;
using Lavalink4NET;

namespace PPMusicBot.Services.Lavalink;
public class LavalinkKenobiBackendTrackSourceService
{
    private readonly IAudioService _audioService;
    private readonly IConfiguration _configuration;
    private readonly ILogger<LavalinkKenobiBackendTrackSourceService> _logger;
    private readonly string _baseUrl;
    private readonly string _apiKey;

    public LavalinkKenobiBackendTrackSourceService(IAudioService audioService, IConfiguration configuration, ILogger<LavalinkKenobiBackendTrackSourceService> logger)
    {
        _audioService = audioService;
        _configuration = configuration;
        _logger = logger;

        var config = _configuration.GetSection("KenobiAPI");
        if (config == null || config["baseUrl"] == null || config["ApiKey"] == null) throw new Exception("KenobiAPI is not configured.");
        _baseUrl = config["baseUrl"]!;
        _apiKey = config["ApiKey"]!;
    }
    public async ValueTask<LavalinkTrack?> LoadTrackAsync(string identifier, CancellationToken cancellationToken = default)
    {
        var url = $"{_baseUrl}/{identifier}";
        _logger.LogInformation(url);
        using var httpClient = new HttpClient();
        httpClient.DefaultRequestHeaders.Add("x-api-key", _apiKey);
        httpClient.DefaultRequestHeaders.Add("x-service-name", "sharing");

        try
        {
            var response = await httpClient.GetAsync(url, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogError($"!200, {response.StatusCode}");
                return null;
            }
            _logger.LogInformation($"200, {response.StatusCode}");
            var options = new TrackLoadOptions() { CacheMode = Lavalink4NET.Rest.Entities.CacheMode.Dynamic, SearchMode = TrackSearchMode.None };
            var track = await _audioService.Tracks.LoadTrackAsync(url, options, cancellationToken: cancellationToken);
            return track;
        }
        catch
        {
            return null;
        }
    }
}