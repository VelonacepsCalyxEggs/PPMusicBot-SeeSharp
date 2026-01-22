using Microsoft.Extensions.Options;
using Microsoft.VisualBasic;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;
using PPMusicBot.Classes;
using PPMusicBot.Models;
using System.Text;

namespace PPMusicBot.Services
{
    public class KenobiAPISearchEngineService
    {
        private readonly ILogger<KenobiAPISearchEngineService> _logger;
        private readonly IConfiguration _configuration;
        private readonly HttpClient _httpClient;
        private readonly string _baseAddress;
        private readonly int LOW_THRESHHOLD;
        private readonly int HIGH_THRESHHOLD;
        private readonly int MAX_SUGGESTIONS;

        public readonly Dictionary<ulong, (KenobiAPISearchResult Result, DateTime Timestamp)> SuggestionCache = [];
        private static readonly TimeSpan CacheTimeout = TimeSpan.FromMinutes(10);

        public KenobiAPISearchEngineService(ILogger<KenobiAPISearchEngineService> logger, IConfiguration configuration)
        {
            _logger = logger;
            _configuration = configuration;

            _httpClient = new HttpClient();

            string? baseAddress = _configuration["KenobiAPI:BaseUrl"];
            ArgumentNullException.ThrowIfNull(baseAddress);
            int lowThreshhold = int.Parse(_configuration["KenobiAPI:SearchEngine:LOW_THRESHOLD"] ?? "200");
            int highThreshhold = int.Parse(_configuration["KenobiAPI:SearchEngine:HIGH_THRESHOLD"] ?? "800");
            int maxSuggestions = int.Parse(_configuration["KenobiAPI:SearchEngine:MAX_SUGGESTIONS"] ?? "5");

            _baseAddress = baseAddress;
            LOW_THRESHHOLD = lowThreshhold;
            HIGH_THRESHHOLD = highThreshhold;
            MAX_SUGGESTIONS = maxSuggestions;
        }
        public async Task<KenobiAPISearchResult?> Search(string query, ulong interactionId)
        {
            var url = _baseAddress + "music/search";
            string jsonString = JsonConvert.SerializeObject(new { query });
            HttpContent content = new StringContent(jsonString, Encoding.UTF8, "application/json");

            HttpResponseMessage? response = await _httpClient.PostAsync(url, content) ?? throw new Exception("Response is null");
            response.EnsureSuccessStatusCode();

            try
            {
                var options = new JsonSerializerSettings()
                {
                    ContractResolver = new CamelCasePropertyNamesContractResolver()
                };
                var parsedData = JsonConvert.DeserializeObject<KenobiAPIModels.SearchResultsDto>(await response.Content.ReadAsStringAsync(), options);

                if (parsedData != null)
                {
                    var parsedResponse = await CalculateResponseAsync(parsedData);
                    if (parsedResponse is not null && parsedResponse.Suggestion)
                    {
                        SuggestionCache.Add(interactionId, (parsedResponse, DateTime.UtcNow));
                        CleanupOldEntries();
                    }
                    return parsedResponse;
                }
                else
                {
                    _logger.LogWarning("Response is null or nothing was found.");
                    return null;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError($"Could not parse API response. {ex.Message} {ex.StackTrace}");
                return null;
            }
        }

        public async Task<KenobiAPISearchResult?> SearchRandom(int amount)
        {
            if (amount < 0) throw new ArgumentOutOfRangeException(nameof(amount));
            if (amount > 100) throw new ArgumentOutOfRangeException(nameof(amount));
            var url = _baseAddress + $"music?SortBy=Random&Limit={amount}";
            HttpResponseMessage? response = await _httpClient.GetAsync(url) ?? throw new Exception("Response is null");
            response.EnsureSuccessStatusCode();
            var options = new JsonSerializerSettings()
            {
                ContractResolver = new CamelCasePropertyNamesContractResolver()
            };
            var parsedData = JsonConvert.DeserializeObject<KenobiAPIModels.ApiResponseDto<List<KenobiAPIModels.MusicTrack>>>(await response.Content.ReadAsStringAsync(), options);

            if (parsedData != null && parsedData.data != null)
            {
                List<KenobiAPIModels.ScoredTrack> tracks = parsedData.data.ToScoredTrack();
                return new KenobiAPISearchResult(tracks, []);
            }
            else
            {
                _logger.LogWarning("Response is null or nothing was found.");
                return null;
            }

        }
        public Uri GetTrackUriFromTrackObject(KenobiAPIModels.MusicTrack track)
        {
            return new Uri(_baseAddress + $"file/createMusicStream/{track.MusicFile[0].Id}");
        }

        private async Task<KenobiAPISearchResult?> CalculateResponseAsync(KenobiAPIModels.SearchResultsDto searchResults)
        {
            double highestScoreTrack = 0;
            double highestScoreAlbum = 0;
            if (searchResults.Tracks.Count != 0) highestScoreTrack =+ searchResults.Tracks[0].Score;
            if (searchResults.Albums.Count != 0) highestScoreAlbum =+ searchResults.Albums[0].Score;

            if (searchResults.Tracks.Count == 0 && searchResults.Albums.Count == 0)
            {
                return null;
            }            

            var slicedTracks = searchResults.Tracks.Skip(1).ToList();
            var slicedAlbums = searchResults.Albums.Skip(1).ToList();

            if (highestScoreAlbum == highestScoreTrack) 
                return new KenobiAPISearchResult(searchResults.Tracks.Slice(0, Math.Min(searchResults.Tracks.Count, MAX_SUGGESTIONS)), searchResults.Albums.Slice(0, Math.Min(searchResults.Albums.Count, MAX_SUGGESTIONS)), true);

            if (highestScoreTrack != 0 && highestScoreTrack > highestScoreAlbum)
            {
                var resultState = DetermineSearchResultState<KenobiAPIModels.ScoredTrack>(slicedTracks, highestScoreTrack, highestScoreAlbum);

                if (resultState == true) return new KenobiAPISearchResult(searchResults.Tracks[..1], []);
                else return new KenobiAPISearchResult(searchResults.Tracks.Slice(0, Math.Min(searchResults.Tracks.Count, MAX_SUGGESTIONS)), searchResults.Albums.Slice(0, Math.Min(searchResults.Albums.Count, MAX_SUGGESTIONS)), true);
                }
            else if (highestScoreAlbum != 0)
            {
                var resultState = DetermineSearchResultState<KenobiAPIModels.ScoredAlbum>(slicedAlbums, highestScoreAlbum, highestScoreTrack);

                if (resultState == true)
                {
                    var parsedData = await RequestAlbumSongsAsync(searchResults.Albums[0].Id);
                    searchResults.Albums[..1][0].Music = parsedData;
                    return new KenobiAPISearchResult([], searchResults.Albums[..1]);
                }
                else return new KenobiAPISearchResult(searchResults.Tracks.Slice(0, Math.Min(searchResults.Tracks.Count, MAX_SUGGESTIONS)), searchResults.Albums.Slice(0, Math.Min(searchResults.Albums.Count, MAX_SUGGESTIONS)), true);
            }
            else
            {
                // Return nothing.
                return null;
            }
        }

        private bool DetermineSearchResultState<T>(List<T> items, double highScorePrimary, double highScoreSecondary) where T : KenobiAPIModels.IScoredItem
        {
            foreach (var item in items)
            {
                if (item.Score == highScorePrimary || item.Score == highScoreSecondary) return false;
            }
            if (highScorePrimary >= HIGH_THRESHHOLD) return true;
            if (highScorePrimary < LOW_THRESHHOLD) return false;
            else return true;
        }

        public async Task<List<KenobiAPIModels.MusicTrack>> RequestAlbumSongsAsync(string albumId)
        {
            var url = _baseAddress + $"/music?AlbumId={albumId}&Limit=0&SortBy=TrackNumber&SortOrder=asc";
            var response = await _httpClient.GetAsync(url);
            response.EnsureSuccessStatusCode();
            var options = new JsonSerializerSettings()
            {
                ContractResolver = new CamelCasePropertyNamesContractResolver()
            };
            var parsedData = JsonConvert.DeserializeObject<KenobiAPIModels.ApiResponseDto<List<KenobiAPIModels.MusicTrack>>>(await response.Content.ReadAsStringAsync(), options);
            if (parsedData == null || parsedData.data == null) throw new NullReferenceException("Album does not contain any songs.");
            return parsedData.data;
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
    };

  

    public class KenobiAPISearchResult(List<KenobiAPIModels.ScoredTrack> Tracks, List<KenobiAPIModels.ScoredAlbum> Albums, bool suggestion = false, Exception? error = null)
    {
        public List<KenobiAPIModels.ScoredTrack> Tracks = Tracks;
        public List<KenobiAPIModels.ScoredAlbum> Albums = Albums;
        public bool Suggestion = suggestion;
        public Exception? Error = error;
    };
}