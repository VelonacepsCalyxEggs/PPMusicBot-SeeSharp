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

namespace PPMusicBot.Services
{
    public class KenobiAPISearchEngineService
    {
        private readonly ILogger<KenobiAPISearchEngineService> _logger;
        private readonly IConfiguration _configuration;

        private readonly string _baseAddress;

        public KenobiAPISearchEngineService(ILogger<KenobiAPISearchEngineService> logger, IConfiguration configuration)
        {
            _logger = logger;
            _configuration = configuration;

            string? baseAddress = _configuration["KenobiAPI:BaseUrl"];
            ArgumentNullException.ThrowIfNull(baseAddress);

            _baseAddress = baseAddress;
        }
        public async Task<KenobiAPISearchResult?> Search(string query)
        {
            var url = _baseAddress + "music/search";
            string jsonString = JsonConvert.SerializeObject(new { query });
            HttpContent content = new StringContent(jsonString, Encoding.UTF8, "application/json");

            using var client = new HttpClient();
            HttpResponseMessage? response = await client.PostAsync(url, content) ?? throw new Exception("Response is null");
            response.EnsureSuccessStatusCode();

            try
            {
                var options = new JsonSerializerSettings()
                {
                    ContractResolver = new CamelCasePropertyNamesContractResolver()
                };
                var parsedData = JsonConvert.DeserializeObject<KenobiAPIModels.SearchResultsDto>(await response.Content.ReadAsStringAsync(), options);

                if (parsedData != null && parsedData.tracks.Count > 0)
                {
                    var objectToPlay = parsedData.tracks[0];
                    string trackUrl = _baseAddress + $"file/createMusicStream/{objectToPlay.MusicFile[0].id}";

                    return new KenobiAPISearchResult(objectToPlay, trackUrl);
                }
                else
                {
                    _logger.LogWarning("Response is null or no tracks were found.");
                    return null;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError($"Could not parse API response. {ex.Message}");
                return null;
            }
        }
    }

    public class KenobiAPISearchResult(KenobiAPIModels.MusicTrack track, string trackUrl)
    {
        public KenobiAPIModels.MusicTrack Track = track;
        public string TrackUrl = trackUrl;
    };
}