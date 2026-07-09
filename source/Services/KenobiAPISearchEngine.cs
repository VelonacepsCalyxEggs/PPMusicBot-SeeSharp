using KenobiRadio.Shared.Models.Radio.Parents;
using KenobiRadio.Shared.Models.Web;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;
using PPMusicBot.Classes;
using PPMusicBot.Models;
using System.Text;
using static PPMusicBot.Commands.SlashCommands.MusicSlashCommandModule.MusicSlashCommandModule;

namespace PPMusicBot.Services
{
    public class KenobiAPISearchEngineService
    {
        private readonly ILogger<KenobiAPISearchEngineService> _logger;
        private readonly HttpClient _httpClient;
        public readonly Dictionary<ulong, (KenobiAPIV2SearchResult Result, DateTime Timestamp)> SuggestionCache = [];
        private static readonly TimeSpan CacheTimeout = TimeSpan.FromMinutes(10);

        public KenobiAPISearchEngineService(ILogger<KenobiAPISearchEngineService> logger)
        {
            _logger = logger;

            _httpClient = new HttpClient();

        }
        public async Task<KenobiAPIV2SearchResult> Search(string title, string? artist, ulong interactionId, SearchType searchType)
        {
            var url = "https://www.funckenobi42.space/api/" + "music" + (searchType == SearchType.Albums ? "/albums/get" : "/tracks/get");
            url = url + $"?{(searchType == SearchType.Albums ? "Name" : "Title")}={title}&Limit=5&Whole=true{(artist != null ? "&Artist=" + artist : string.Empty)}";
            HttpResponseMessage? response = await _httpClient.GetAsync(url) ?? throw new Exception("Response is null");
            if (!response.IsSuccessStatusCode)
            {
                throw new Exception($"Could not reach the API, error: {response.StatusCode}");
            }
            try
            {
                if (searchType == SearchType.Tracks)
                {
                    var options = new JsonSerializerSettings()
                    {
                        ContractResolver = new CamelCasePropertyNamesContractResolver()
                    };
                    var content = await response.Content.ReadAsStringAsync();
                    if (string.IsNullOrEmpty(content))
                        throw new ArgumentNullException($"Content of the response was empty, report this error to the developer. {DateTime.UtcNow.ToShortTimeString()}");
                    var parsedData = JsonConvert.DeserializeObject<ResponseWrapper<List<TrackParentDto>>>(content, options);

                    if (parsedData != null)
                    {
                        var result = new KenobiAPIV2SearchResult(parsedData.Result, [], suggestion: false, null);
                        if (parsedData.Result.Count > 1)
                        {
                            result.Suggestion = true;
                            SuggestionCache.Add(interactionId, (result, DateTime.UtcNow));
                            CleanupOldEntries();
                            return result;
                        }
                        if (parsedData.Result.Count > 5)
                            throw new ArgumentOutOfRangeException("API returned more than 5 items.");
                        return result;
                    }
                    else
                    {
                        _logger.LogWarning("Response is null or nothing was found.");
                        throw new Exception("API returned an empty response.");
                    }
                }
                else
                {
                    var options = new JsonSerializerSettings()
                    {
                        ContractResolver = new CamelCasePropertyNamesContractResolver()
                    };
                    var parsedData = JsonConvert.DeserializeObject<ResponseWrapper<List<AlbumParentDto>>>(await response.Content.ReadAsStringAsync(), options);

                    if (parsedData != null)
                    {
                        var result = new KenobiAPIV2SearchResult([], parsedData.Result, suggestion: false, null);
                        if (parsedData.Result.Count > 1)
                        {
                            result.Suggestion = true;
                            SuggestionCache.Add(interactionId, (result, DateTime.UtcNow));
                            CleanupOldEntries();
                            return result;
                        }
                        if (parsedData.Result.Count > 5)
                            throw new ArgumentOutOfRangeException("API returned more than 5 items.");
                        return result;
                    }
                    else
                    {
                        _logger.LogWarning("Response is null or nothing was found.");
                        throw new Exception("API returned an empty response.");
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError($"Could not parse API response. {ex.Message} {ex.StackTrace}");
                throw;
            }
        }
        public async Task<KenobiAPIV2SearchResult> SearchRandom(int amount)
        {
            var url = "https://www.funckenobi42.space/api/" + "music/tracks/get";
            url = url + $"?Limit={amount}&Whole=true&OrderBy=Random";
            HttpResponseMessage? response = await _httpClient.GetAsync(url) ?? throw new Exception("Response is null");
            response.EnsureSuccessStatusCode();
            try
            {
                var options = new JsonSerializerSettings()
                {
                    ContractResolver = new CamelCasePropertyNamesContractResolver()
                };
                var parsedData = JsonConvert.DeserializeObject<ResponseWrapper<List<TrackParentDto>>>(await response.Content.ReadAsStringAsync(), options);

                if (parsedData != null)
                {
                    var result = new KenobiAPIV2SearchResult(parsedData.Result, [], suggestion: false, null);
                    if (parsedData.Result.Count > 1)
                    {
                        return result;
                    }
                    if (parsedData.Result.Count > 5)
                        throw new ArgumentOutOfRangeException("API returned more than 5 items.");
                    return result;
                }
                else
                {
                    _logger.LogWarning("Response is null or nothing was found.");
                    throw new Exception("API returned an empty response.");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError($"Could not parse API response. {ex.Message} {ex.StackTrace}");
                throw;
            }
        }
        private void CleanupOldEntries()
        {
            var now = DateTime.UtcNow;
            var oldKeys = SuggestionCache.Where(x => now - x.Value.Timestamp > CacheTimeout)
                                         .Select(x => x.Key)
                                         .ToList();

            foreach (var key in oldKeys)
            {
                SuggestionCache.Remove(key);
            }
        }

        public class KenobiAPIV2SearchResult(List<TrackParentDto> Tracks, List<AlbumParentDto> Albums, bool suggestion = false, Exception? error = null)
        {
            public List<TrackParentDto> Tracks = Tracks;
            public List<AlbumParentDto> Albums = Albums;
            public bool Suggestion = suggestion;
            public Exception? Error = error;
        };
    }
}