using Discord;
using Discord.Interactions;
using Lavalink4NET.Extensions;
using Lavalink4NET.Players.Vote;
using Lavalink4NET.Rest.Entities.Tracks;
using Lavalink4NET.Tracks;
using PPMusicBot.Models;
using PPMusicBot.Services;
using System.Diagnostics.Eventing.Reader;
using System.Text;
using static PPMusicBot.Helpers.Helpers;
namespace PPMusicBot.Commands.SlashCommands.MusicSlashCommandModule
{
    public sealed partial class MusicSlashCommandModule
    {

        [SlashCommand("file", description: "Plays a file you attach, only mp3,mp4,wav,oggs", runMode: RunMode.Async)]
        public async Task PlayAttachment(Attachment file)
        {
            try
            {
                await DeferAsync().ConfigureAwait(false);
                if (CheckIfValidContentType(file.ContentType))
                {
                    var player = await GetPlayerAsync(connectToVoiceChannel: true).ConfigureAwait(false);

                    if (player is null)
                    {
                        await FollowupAsync("Failed to connect to voice channel.").ConfigureAwait(false);
                        return;
                    }
                    var tracks = await _audioService.Tracks.LoadTracksAsync(file.Url, TrackSearchMode.None);

                    if (tracks.Track is null)
                    {
                        await FollowupAsync("Lavalink could not load the attachment.").ConfigureAwait(false);
                        return;
                    }

                    var position = await player.PlayAsync(tracks).ConfigureAwait(false);

                    await FollowupAsync(embed: await BuildPlayingEmbed(position, tracks, null, _artworkService)).ConfigureAwait(false);
                    return;
                }
                else
                {
                    _logger.LogWarning($"Attachment of non audio type: {file.ContentType}");
                    await FollowupAsync("Sorry, but your file is not a mp3, mp4, wav or a ogg.");
                    return;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, ex.Message);
                throw;
            }
        }
        /// <summary>
        ///     Plays music asynchronously.
        /// </summary>
        /// <param name="query">the search query</param>
        /// <returns>a task that represents the asynchronous operation</returns>
        [SlashCommand("play", description: "Plays music, from everywhere.", runMode: RunMode.Async)]
        public async Task PlayAsync(string query, bool shuffle = false)
        {
            try
            {
                await DeferAsync().ConfigureAwait(false);

                var player = await GetPlayerAsync(connectToVoiceChannel: true).ConfigureAwait(false);

                if (player is null)
                    return;

                PlayQuery? playQuery = DetermineQueryType(query) switch
                {
                    PlayQueryType.None => null,
                    PlayQueryType.Youtube => FormYoutubeQuery(query, new Uri(query)),
                    PlayQueryType.YoutubeSearch => FormYoutubeQuery(query, null),
                    PlayQueryType.External => FormExternalQuery(query, new Uri(query)),
                    _ => throw new NotImplementedException()
                };

                if (playQuery == null)
                {
                    throw new ArgumentNullException(nameof(playQuery));
                }

                TrackLoadResult result;
                if (playQuery.QueryType == PlayQueryType.External)
                {
                    result = await _audioService.Tracks.LoadTracksAsync(playQuery.Query, new TrackLoadOptions(playQuery.SearchMode, StrictSearchBehavior.Resolve)).ConfigureAwait(false);
                }
                else
                {
                    result = await _audioService.Tracks.LoadTracksAsync(playQuery.Query, new TrackLoadOptions(playQuery.SearchMode, StrictSearchBehavior.Throw)).ConfigureAwait(false);
                }

                if (!result.HasMatches)
                {
                    await FollowupAsync("We could not find any tracks that fit the criteria.").ConfigureAwait(false);
                    return;
                }
                IEnumerable<LavalinkTrack>? shuffledTracks = null;
                if (shuffle)
                {
                    shuffledTracks = result.Tracks.Shuffle();
                }

                
                await FollowupAsync(embed: await BuildPlayingEmbed(player.Queue.Count, result, null, _artworkService)).ConfigureAwait(false);
                if (playQuery.IsPlaylist || result.IsPlaylist)
                {
                    foreach (var track in shuffledTracks ?? result.Tracks)
                    {
                        await player.PlayAsync(track);
                    }
                }
                else
                {
                    await player.PlayAsync(result.Track!);
                }
                return;
            }
            catch (Exception ex)
            {
                _logger.LogError($"{ex.Message} {ex.StackTrace}");
                throw;
            }
        }
        public enum SearchType
        {
            [ChoiceDisplay("Any")]
            Any,
            [ChoiceDisplay("Tracks")]
            Tracks,
            [ChoiceDisplay("Albums")]
            Albums
        }
        [SlashCommand("fromdb", description: "Plays music from database only.", runMode: RunMode.Async)]
        public async Task PlayFromDbAsync(
            string query,
            SearchType searchType = SearchType.Any,
            bool shuffle = false)
        {
            try
            {
                await DeferAsync().ConfigureAwait(false);

                var player = await GetPlayerAsync(connectToVoiceChannel: true).ConfigureAwait(false);

                if (player is null)
                    return;

                var result = await _kenobiAPISearchEngineService.Search(query, Context.Interaction.Id).ConfigureAwait(false);

                if (result is null)
                {
                    await FollowupAsync("The database did not find any tracks.").ConfigureAwait(false);
                    return;
                }
                // REMOVE THIS LATER!!!
                // NEEDS BACKEND CHANGES.
                // IF NOT REMOVED IN 1 MONTH ADMINISTER BROMINE HEXAFLUORIDE TO REPOSITORY OWNER
                if (searchType == SearchType.Tracks)
                {
                    result.Albums.Clear();
                    if (result.Tracks.Count() == 1)
                    {
                        await PlayDatabaseTracks(player, result, shuffle: shuffle, searchType: searchType);
                        return;
                    }
                    else if (result.Tracks.Count() != 0)
                    {
                        result.Suggestion = true;
                    }
                    else
                    {
                        await FollowupAsync($"Could not find any matching {(searchType != SearchType.Any ? searchType.ToString() : "entries")} in DB.");
                        return;
                    }
                }
                else if (searchType == SearchType.Albums)
                {
                    result.Tracks.Clear();
                    if (result.Albums.Count() == 1)
                    {
                        await PlayDatabaseTracks(player, result, shuffle: shuffle, searchType: searchType);
                        return;
                    }
                    else if (result.Albums.Count() != 0)
                    {
                        result.Suggestion = true;
                    }
                    else
                    {
                        await FollowupAsync($"Could not find any matching {(searchType != SearchType.Any ? searchType.ToString() : "entries")} in DB.");
                        return;
                    }
                }
                if (result.Suggestion)
                {
                    var menuBuilder = new SelectMenuBuilder()
                        .WithPlaceholder("Select an option")
                        .WithCustomId($"suggestion_selector:{Context.Interaction.Id}")
                        .WithMinValues(1)
                        .WithMaxValues(1);

                    StringBuilder sb = new StringBuilder();
                    sb.AppendLine("Maybe you meant:");

                    if (result.Tracks.Count > 0)
                    {
                        sb.AppendLine("**Tracks:**");
                        var trackCount = Math.Min(result.Tracks.Count, 15);
                        for (var i = 0; i < trackCount; i++)
                        {
                            sb.AppendLine($"{result.Tracks[i].Title} - Score: {result.Tracks[i].Score}");
                            menuBuilder.AddOption($"Track: {result.Tracks[i].Title}", $"track_{i}");
                        }
                    }

                    if (result.Albums.Count > 0 && menuBuilder.Options.Count < 25)
                    {
                        sb.AppendLine("**Albums:**");
                        var remainingSlots = 25 - menuBuilder.Options.Count;
                        var albumCount = Math.Min(result.Albums.Count, remainingSlots);
                        for (var i = 0; i < albumCount; i++)
                        {
                            sb.AppendLine($"{result.Albums[i].Name} - Score: {result.Albums[i].Score}");
                            menuBuilder.AddOption($"Album: {result.Albums[i].Name}", $"album_{i}");
                        }
                    }

                    var embed = new EmbedBuilder()
                    {
                        Title = "Oh oh! We are unsure about what you want...",
                        Description = sb.ToString(),
                        Footer = new EmbedFooterBuilder() { Text = $"Try using quotes to get more exact results for your query." }
                    }.Build();

                    var components = new ComponentBuilder()
                        .WithSelectMenu(menuBuilder).Build();
                    await FollowupAsync(embed: embed, components: components).ConfigureAwait(false);
                    return;
                }

                await PlayDatabaseTracks(player, result, shuffle: shuffle, searchType: searchType);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, ex.Message);
                throw;
            }
        }
        [SlashCommand("fromdbrandom", description: "Plays random music from database only.", runMode: RunMode.Async)]
        public async Task PlayRandomFromDbAsync(
            [Summary("amount", "Amount of tracks to be queried.")]
            [MinValue(1)]
            [MaxValue(100)]
            int amount
            )
        {
            try
            {
                await DeferAsync().ConfigureAwait(false);

                var player = await GetPlayerAsync(connectToVoiceChannel: true).ConfigureAwait(false);

                if (player is null)
                    return;

                var result = await _kenobiAPISearchEngineService.SearchRandom(amount).ConfigureAwait(false);

                if (result is null)
                {
                    await FollowupAsync("The database did not find any tracks.").ConfigureAwait(false);
                    return;
                }

                await PlayDatabaseTracks(player, result, playAllTracks: true);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, ex.Message);
                throw;
            }
        }
        private async Task PlayDatabaseTracks(
            VoteLavalinkPlayer player, 
            KenobiAPISearchResult result, 
            int wantedTrackIndex = 0, 
            int wantedAlbumIndex = 0, 
            bool doModifyOriginalResponse = false, 
            bool shuffle = false, 
            SearchType searchType = SearchType.Any,
            bool playAllTracks = false)
        {
            _logger.LogInformation($"Playing {result.Tracks.Count()} tracks and {result.Albums.Count()} albums from database.");
            if ((result.Tracks.Count() > 0 && result.Albums.Count() == 0) 
                && (searchType == SearchType.Tracks || searchType == SearchType.Any))
            {
                var trackURL = _kenobiAPISearchEngineService.GetTrackUriFromTrackObject(result.Tracks[wantedTrackIndex]).OriginalString;
                _logger.LogDebug($"Loading from URL: {trackURL}");
                var tracks = await _audioService.Tracks.LoadTracksAsync(trackURL, TrackSearchMode.None);
                if (tracks.Track is null)
                {
                    await FollowupAsync("Lavalink could not load the track.");
                    return;
                }
                if (!playAllTracks)
                    result.Tracks = [result.Tracks[wantedTrackIndex]];
                if (shuffle)
                {
                    result.Tracks = result.Tracks.Shuffle().ToList();
                }
                var position = await player.PlayAsync(new CustomQueueTrackItem(tracks.Track, result.Tracks[0])).ConfigureAwait(false);

                if (playAllTracks)
                {
                    foreach (var track in result.Tracks[1..])
                    {
                        var loaded = await _audioService.Tracks.LoadTrackAsync(_kenobiAPISearchEngineService.GetTrackUriFromTrackObject(track).OriginalString, TrackSearchMode.None);
                        if (loaded is null) { continue; }
                        await player.PlayAsync(new CustomQueueTrackItem(loaded, track)).ConfigureAwait(false);
                    }
                }

                if (!doModifyOriginalResponse) await FollowupAsync(embed: await BuildPlayingEmbed(position, tracks, result, null)).ConfigureAwait(false);
                else await ModifyOriginalResponseAsync(async msg =>
                {
                    msg.Embed = await BuildPlayingEmbed(position, tracks, result, null);
                    msg.Components = new ComponentBuilder().Build();
                }).ConfigureAwait(false);
            }
            else if (result.Albums.Count() > 0 && (searchType == SearchType.Albums || searchType == SearchType.Any))
            {
                if (wantedAlbumIndex != 0)
                {
                    result.Albums = [result.Albums[wantedAlbumIndex]];
                }
                if (result.Albums[0].Music.Count() == 0)
                {
                    result.Albums[0].Music = await _kenobiAPISearchEngineService.RequestAlbumSongsAsync(result.Albums[0].Id);
                }
                if (shuffle)
                {
                    result.Albums[0].Music = result.Albums[0].Music.Shuffle().ToList();
                }
                var firstToPlay = await _audioService.Tracks.LoadTrackAsync(_kenobiAPISearchEngineService.GetTrackUriFromTrackObject(result.Albums[0].Music[0]).OriginalString, TrackSearchMode.None);
                if (firstToPlay is null)
                {
                    await FollowupAsync("Huh? The first track in the sequence is not available? Aborting.");
                    return;
                }
                var position = await player.PlayAsync(new CustomQueueTrackItem(firstToPlay, result.Albums[0].Music[0])).ConfigureAwait(false);

                if (!doModifyOriginalResponse) await FollowupAsync(embed: await BuildPlayingEmbed(position, null, result, null)).ConfigureAwait(false);
                else await ModifyOriginalResponseAsync(async msg =>
                {
                    msg.Embed = await BuildPlayingEmbed(position, null, result, null);
                    msg.Components = new ComponentBuilder().Build();
                }).ConfigureAwait(false);
                foreach (var resultTrack in result.Albums[0].Music[1..])
                {
                    var track = await _audioService.Tracks.LoadTrackAsync(_kenobiAPISearchEngineService.GetTrackUriFromTrackObject(resultTrack).OriginalString, TrackSearchMode.None);
                    if (track is null) { continue; }
                    await player.PlayAsync(new CustomQueueTrackItem(track, resultTrack)).ConfigureAwait(false);
                }
            }
            else
            {
                await FollowupAsync($"Could not find any matching {(searchType != SearchType.Any ? searchType.ToString() : "entries")} in the database.");
            }
        }

        [ComponentInteraction("suggestion_selector:*")]
        public async Task HandleSuggestionSelection(ulong interactionId, string[] selectedOptions)
        {
            try
            {
                await DeferAsync().ConfigureAwait(false);
                if (!_kenobiAPISearchEngineService.SuggestionCache.TryGetValue(interactionId, out (KenobiAPISearchResult Result, DateTime Timestamp) value))
                {
                    await FollowupAsync("This suggestion menu has expired. Please try your search again.").ConfigureAwait(false);
                    return;
                }
                var result = value.Result;
                if (result == null)
                {
                    await FollowupAsync("This suggestion menu has expired. Please try your search again.").ConfigureAwait(false);
                    return;
                }

                var selectedValue = selectedOptions.First();
                var player = await GetPlayerAsync(connectToVoiceChannel: true).ConfigureAwait(false);

                if (player is null)
                {
                    await FollowupAsync("Unable to connect to voice channel.").ConfigureAwait(false);
                    return;
                }
                // Parse the selection
                if (selectedValue.StartsWith("track_"))
                {
                    var trackIndex = int.Parse(selectedValue.Substring(6));
                    result.Albums.Clear();
                    await PlayDatabaseTracks(player, result, wantedTrackIndex: trackIndex, doModifyOriginalResponse: true);
                }
                else if (selectedValue.StartsWith("album_"))
                {
                    var albumIndex = int.Parse(selectedValue.Substring(6));
                    result.Tracks.Clear();
                    await PlayDatabaseTracks(player, result, wantedAlbumIndex: albumIndex, doModifyOriginalResponse: true);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError($"{ex.Message} {ex.StackTrace}");
                await FollowupAsync("An error occurred while processing your selection.").ConfigureAwait(false);
                await ModifyOriginalResponseAsync(msg =>
                {
                    msg.Components = new ComponentBuilder().Build();
                }).ConfigureAwait(false);
            }
            finally
            {
                _kenobiAPISearchEngineService.SuggestionCache.Remove(interactionId);
            }
        }
        private PlayQueryType DetermineQueryType(string query)
        {
            try
            {
                var uriFromQuery = new Uri(query);
                if (uriFromQuery.Host == "www.youtube.com" || uriFromQuery.Host == "youtu.be" || uriFromQuery.Host == "music.youtube.com")
                {
                    return PlayQueryType.Youtube;
                }
                else
                {
                    return PlayQueryType.External;
                }
            }
            catch (UriFormatException)
            {
                _logger.LogWarning("Could not form Uri from query, attempting to do a direct Youtube search.");
                return PlayQueryType.YoutubeSearch;
            }
        }

        private static PlayQuery FormYoutubeQuery(string query, Uri? uri)
        {
            if (uri != null)
            {
                if (uri.Query.Contains("?list=") || uri.Query.Contains("&list="))
                {
                    return new PlayQuery()
                    {
                        Query = query,
                        Uri = uri,
                        SearchMode = TrackSearchMode.YouTube,
                        QueryType = PlayQueryType.Youtube,
                        IsPlaylist = true,
                        ModifyOriginalResponse = false,
                    };
                }
                else 
                {
                    return new PlayQuery()
                    {
                        Query = query,
                        Uri = uri,
                        SearchMode = TrackSearchMode.YouTube,
                        QueryType = PlayQueryType.Youtube,
                        IsPlaylist = false,
                        ModifyOriginalResponse = false,
                    };
                }
            }
            else
            {
                return new PlayQuery()
                {
                    Query = query,
                    Uri = null,
                    SearchMode = TrackSearchMode.YouTube,
                    QueryType = PlayQueryType.YoutubeSearch,
                    IsPlaylist = false,
                    ModifyOriginalResponse = false,
                };
            }
        }

        private static PlayQuery FormExternalQuery(string query, Uri uri)
        {
            if (uri.Scheme == "icecast")
            {
                return new PlayQuery()
                {
                    Query = query,
                    Uri = uri,
                    SearchMode = TrackSearchMode.None,
                    QueryType = PlayQueryType.External,
                    IsPlaylist = false,
                    ModifyOriginalResponse = false,
                };
            }
            else
            {
                return new PlayQuery()
                {
                    Query = query,
                    Uri = uri,
                    SearchMode = TrackSearchMode.None,
                    QueryType = PlayQueryType.External,
                    IsPlaylist = false,
                    ModifyOriginalResponse = false,
                };
            }
        }

        public enum PlayQueryType
        {
            None,
            Youtube,
            YoutubeSearch,
            External
        }

        public record PlayQuery
        {
            public required string Query { get; init; }
            public Uri? Uri { get; init; }
            public TrackSearchMode SearchMode { get; init; }
            public PlayQueryType QueryType { get; init; }
            public bool IsPlaylist { get; init; }
            public bool ModifyOriginalResponse { get; init; }
            public PlayQuery(string query, Uri? uri, TrackSearchMode searchMode, PlayQueryType queryType, bool isPlaylist, bool isLive, bool modifyOriginalResponse)
            {
                Query = query;
                Uri = uri;
                SearchMode = searchMode;
                QueryType = queryType;
                IsPlaylist = isPlaylist;
                ModifyOriginalResponse = modifyOriginalResponse;
            }

            public PlayQuery()
            {
            }
        }
    }
}