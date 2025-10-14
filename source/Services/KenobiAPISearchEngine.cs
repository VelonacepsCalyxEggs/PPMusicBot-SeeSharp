using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http.Json;
using System.Text;
using System.Threading.Tasks;
using PPMusicBot.Models;
using System.Diagnostics;
using Newtonsoft.Json.Serialization;
using Lavalink4NET.Tracks;
using Lavalink4NET.Rest.Entities.Tracks;
using Lavalink4NET;
using System.Collections.Immutable;
using System.Text.Json;
using System.Diagnostics.Eventing.Reader;
using Microsoft.Extensions.Options;

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
        public async Task<KenobiAPISearchResult?> Search(string query)
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
                    return await CalculateResponseAsync(parsedData);
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
        public Uri GetTrackUriFromTrackObject(KenobiAPIModels.MusicTrack track)
        {
            return new Uri(_baseAddress + $"file/createMusicStream/{track.MusicFile[0].id}");
        }

        private async Task<KenobiAPISearchResult?> CalculateResponseAsync(KenobiAPIModels.SearchResultsDto searchResults)
        {
            double highestScoreTrack = 0;
            double highestScoreAlbum = 0;
            if (searchResults.tracks.Count != 0) highestScoreTrack =+ searchResults.tracks[0].score;
            if (searchResults.albums.Count != 0) highestScoreAlbum =+ searchResults.albums[0].score;

            var slicedTracks = searchResults.tracks.Skip(1).ToList();
            var slicedAlbums = searchResults.albums.Skip(1).ToList();
            if (highestScoreTrack != 0 && highestScoreTrack > highestScoreAlbum)
            {
                var resultState = DetermineSearchResultState<KenobiAPIModels.ScoredTrack>(slicedTracks, highestScoreTrack);

                if (resultState == true) return new KenobiAPISearchResult(searchResults.tracks[..1], []);
                else return new KenobiAPISearchResult(searchResults.tracks, searchResults.albums, true);
            }
            else if (highestScoreAlbum != 0)
            {
                var resultState = DetermineSearchResultState<KenobiAPIModels.ScoredAlbum>(slicedAlbums, highestScoreAlbum);

                if (resultState == true)
                {

                    // This should be a separate function.
                    var url = _baseAddress + $"/music?albumId={searchResults.albums[0].id}&limit=0&sortBy=trackNumber&sortOrder=asc";
                    var response = await _httpClient.GetAsync(url);
                    response.EnsureSuccessStatusCode();
                    var options = new JsonSerializerSettings()
                    {
                        ContractResolver = new CamelCasePropertyNamesContractResolver()
                    };
                    var parsedData = JsonConvert.DeserializeObject<KenobiAPIModels.ApiResponseDto<List<KenobiAPIModels.MusicTrack>>>(await response.Content.ReadAsStringAsync(), options);
                    if (parsedData == null || parsedData.data == null) throw new NullReferenceException("Album does not contain any songs.");
                    searchResults.albums[..1][0].Music = parsedData.data; 
                    return new KenobiAPISearchResult([], searchResults.albums[..1]);
                }
                else return new KenobiAPISearchResult(searchResults.tracks.Slice(0, Math.Min(searchResults.tracks.Count, MAX_SUGGESTIONS)), searchResults.albums.Slice(0, Math.Min(searchResults.albums.Count, MAX_SUGGESTIONS)), true);
            }
            else
            {
                // Return nothing.
                return null;
            }
        }

        private bool DetermineSearchResultState<T>(List<T> items, double highestScoreItem) where T : KenobiAPIModels.IScoredItem
        {
            foreach (var item in items)
            {
                if (item.score == highestScoreItem) return false;
            }
            if (highestScoreItem >= HIGH_THRESHHOLD) return true;
            if (highestScoreItem < LOW_THRESHHOLD) return false;
            else return true;
        }
    };

  

    public class KenobiAPISearchResult(List<KenobiAPIModels.ScoredTrack> tracks, List<KenobiAPIModels.ScoredAlbum> albums, bool suggestion = false, Exception? error = null)
    {
        public List<KenobiAPIModels.ScoredTrack> Tracks = tracks;
        public List<KenobiAPIModels.ScoredAlbum> Albums = albums;
        public bool Suggestion = suggestion;
        public Exception? Error = error;
    };
}