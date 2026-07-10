using Discord;
using Discord.Interactions;
using Lavalink4NET.Extensions;
using Lavalink4NET.Players.Vote;
using Lavalink4NET.Rest.Entities.Tracks;
using Lavalink4NET.Tracks;
using PPMusicBot.Models;
using PPMusicBot.Services;
using System.Diagnostics;
using System.Text;
using static PPMusicBot.Helpers.Helpers;
using static PPMusicBot.Services.KenobiAPISearchEngineService;
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

                    await player.PlayAsync(tracks).ConfigureAwait(false);

                    await FollowupAsync(embed: await BuildPlayingEmbed(player.Queue.Count, player.State, lavalinkResult: tracks, artworkService: _artworkService).ConfigureAwait(false)).ConfigureAwait(false);
                    return;
                }
                else
                {
                    _logger.LogWarning($"Attachment of non audio type: {file.ContentType}");
                    await FollowupAsync("Sorry, but your file is not a mp3, mp4, wav or a ogg.").ConfigureAwait(false);
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
                if (playQuery.QueryType == PlayQueryType.External) // If is external, we try to resolve.
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

                
                await FollowupAsync(embed: await BuildPlayingEmbed(player.Queue.Count, player.State, lavalinkResult: result, artworkService: _artworkService).ConfigureAwait(false)).ConfigureAwait(false);
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
            [ChoiceDisplay("Tracks")]
            Tracks,
            [ChoiceDisplay("Albums")]
            Albums
        }
        /// <summary>
        ///     Plays music from database asynchronously.
        ///     IMPORTANT: First builds embed, then loads tracks.
        /// </summary>
        /// <param name="query">the search query</param>
        /// <returns>a task that represents the asynchronous operation</returns>
        [SlashCommand("fromdb", description: "Plays music from database only.", runMode: RunMode.Async)]
        public async Task PlayFromDbAsync(
            string title, string? artist = null,
            SearchType searchType = SearchType.Tracks,
            bool shuffle = false)
        {
            try
            {
                await DeferAsync().ConfigureAwait(false);

                var player = await GetPlayerAsync(connectToVoiceChannel: true).ConfigureAwait(false);

                if (player is null)
                    return;
                KenobiAPIV2SearchResult result;
                try
                {
                    result = await _kenobiAPISearchEngineService.Search(title, artist, Context.Interaction.Id, searchType).ConfigureAwait(false);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "API Search failed.");
                    await FollowupAsync(ex.Message).ConfigureAwait(false);
                    return;
                }
                if (result is null)
                {
                    await FollowupAsync("The database did not find any tracks.").ConfigureAwait(false);
                    return;
                }
                _logger.LogDebug("RESULT:");
                _logger.LogDebug("TRACKS: " + result.Tracks.Count());
                _logger.LogDebug("ALBUMS: " + result.Albums.Count());
                _logger.LogDebug("IS SUGGESTION: " + result.Suggestion);

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
                            sb.AppendLine($"{result.Tracks[i].Title} by {string.Join(',', result.Tracks[i].Artists.Select(a => a.NameTransliterated))}");
                            menuBuilder.AddOption($"Track: {result.Tracks[i].TitleTransliterated} by {result.Tracks[i].Artists.FirstOrDefault()!.NameTransliterated}", $"track_{i}");
                        }
                    }

                    if (result.Albums.Count > 0 && menuBuilder.Options.Count < 25)
                    {
                        sb.AppendLine("**Albums:**");
                        var remainingSlots = 25 - menuBuilder.Options.Count;
                        var albumCount = Math.Min(result.Albums.Count, remainingSlots);
                        for (var i = 0; i < albumCount; i++)
                        {
                            sb.AppendLine($"{result.Albums[i].NameTransliterated}");
                            menuBuilder.AddOption($"Album: {result.Albums[i].Name}", $"album_{i}");
                        }
                    }

                    var embed = new EmbedBuilder()
                    {
                        Title = "Oh oh! We are unsure about what you want...",
                        Description = sb.ToString(),
                        Footer = new EmbedFooterBuilder() { Text = $"Try using quotes to get more exact results for your query." }
                    }.Build();

                    var cancelButton = new ButtonBuilder()
                        .WithCustomId($"cancel_suggestion:{Context.Interaction.Id}")
                        .WithLabel("Cancel")
                        .WithStyle(ButtonStyle.Danger);


                    var components = new ComponentBuilder()
                        .WithSelectMenu(menuBuilder)
                        .WithButton(cancelButton)
                        .Build();
                    await FollowupAsync(embed: embed, components: components).ConfigureAwait(false);
                    return;
                }

                await PlayDatabaseTracks(player, result, shuffle: shuffle).ConfigureAwait(false);
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
            [MaxValue(5)]
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

                if (result is null || result.Tracks.Count == 0 && result.Albums.Count == 0)
                {
                    await FollowupAsync("The database did not find any tracks.").ConfigureAwait(false);
                    return;
                }

                await PlayDatabaseTracks(player, result, playAllTracks: true).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, ex.Message);
                throw;
            }
        }
        private async Task PlayDatabaseTracks(
            VoteLavalinkPlayer player,
            KenobiAPIV2SearchResult result,
            bool doModifyOriginalResponse = false,
            bool shuffle = false, bool playAllTracks = false)
        {
            if (result.Tracks.Count > 1 && playAllTracks)
            {

                if (!doModifyOriginalResponse) await FollowupAsync(embed: new EmbedBuilder() { Title = $"Selected {result.Tracks.Count} random tracks from the database.", Description = "Use /queue to view what tracks were added."}.Build()).ConfigureAwait(false);
                else await ModifyOriginalResponseAsync(async msg =>
                {
                    msg.Embed = await BuildPlayingEmbed(player.Queue.Count, player.State, artworkService: _artworkService, null, result).ConfigureAwait(false); ;
                    msg.Components = new ComponentBuilder().Build();
                }).ConfigureAwait(false);
                for (int i = 0; i < result.Tracks.Count; i++)
                {
                    var dbTrack = result.Tracks[i];
                    var loadedTrack = await _audioService.Tracks.LoadTracksAsync($"https://www.funckenobi42.space/api/files/stream/{dbTrack.MusicFile.Id.ToString()}", TrackSearchMode.None);
                    if (loadedTrack.Track is null)
                    {
                        await ModifyOriginalResponseAsync(async msg => await FollowupAsync($"Lavalink could not load the track with url {$"https://www.funckenobi42.space/api/files/stream/{dbTrack.MusicFile.Id.ToString()}"}").ConfigureAwait(false)).ConfigureAwait(false);
                        return;
                    }
                    var position = await player.PlayAsync(new CustomQueueTrackItem(loadedTrack.Track, dbTrack)).ConfigureAwait(false);
                }
                return;
            } 
            var track = result.Tracks.FirstOrDefault();
            if (track is not null)
            {
                var tracks = await _audioService.Tracks.LoadTracksAsync($"https://www.funckenobi42.space/api/files/stream/{track.MusicFile.Id.ToString()}", TrackSearchMode.None);
                if (tracks.Track is null)
                {
                    await ModifyOriginalResponseAsync(msg =>
                    {
                        msg.Content = "Lavalink could not load the track.";
                    }).ConfigureAwait(false);
                    return;
                }
                if (!doModifyOriginalResponse) await FollowupAsync(embed: await BuildPlayingEmbed(player.Queue.Count, player.State, _artworkService, null, result).ConfigureAwait(false)).ConfigureAwait(false);
                else await ModifyOriginalResponseAsync(async msg =>
                {
                    msg.Embed = await BuildPlayingEmbed(player.Queue.Count, player.State, artworkService: _artworkService, null, result).ConfigureAwait(false); ;
                    msg.Components = new ComponentBuilder().Build();
                }).ConfigureAwait(false);
                var position = await player.PlayAsync(new CustomQueueTrackItem(tracks.Track, track)).ConfigureAwait(false);
                return;
            }
            else
            {
                var album = result.Albums.FirstOrDefault();
                if (album is null)
                {
                    await ModifyOriginalResponseAsync(async msg => await FollowupAsync("There was no album to load.").ConfigureAwait(false)).ConfigureAwait(false);
                    return;
                }
                if (!doModifyOriginalResponse) await FollowupAsync(embed: await BuildPlayingEmbed(player.Queue.Count, player.State, _artworkService, null, result).ConfigureAwait(false)).ConfigureAwait(false);
                else await ModifyOriginalResponseAsync(async msg =>
                {
                    msg.Embed = await BuildPlayingEmbed(player.Queue.Count, player.State, _artworkService, null, result).ConfigureAwait(false);
                    msg.Components = new ComponentBuilder().Build();
                }).ConfigureAwait(false);
                foreach (var disc in album.Discs)
                {
                    foreach (var dbTrack in disc.Tracks)
                    {
                        var loaded = await _audioService.Tracks.LoadTrackAsync($"https://www.funckenobi42.space/api/files/stream/{dbTrack.MusicFile.Id.ToString()}", TrackSearchMode.None).ConfigureAwait(false);
                        if (loaded is null) { continue; }
                        await player.PlayAsync(new CustomQueueTrackItem(loaded, dbTrack)).ConfigureAwait(false);
                    }

                }
                return;
            }
        }

        [ComponentInteraction("cancel_suggestion:*")]
        public async Task HandleCancelSuggestion(ulong interactionId)
        {
            try
            {
                await DeferAsync().ConfigureAwait(false);
                if (_kenobiAPISearchEngineService.SuggestionCache.TryGetValue(interactionId, out var result1)) {
                    _kenobiAPISearchEngineService.SuggestionCache.Remove(interactionId);
                }
                await ModifyOriginalResponseAsync(msg =>
                {
                    msg.Content = "Suggestion cancelled.";
                    msg.Embed = null;
                    msg.Components = new ComponentBuilder().Build();
                }).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error cancelling suggestion menu.");
                await FollowupAsync("An error occurred while cancelling.").ConfigureAwait(false);
            }
        }
        [ComponentInteraction("suggestion_selector:*")]
        public async Task HandleSuggestionSelection(ulong interactionId, string[] selectedOptions)
        {
            try
            {
                await DeferAsync().ConfigureAwait(false);
                if (_kenobiAPISearchEngineService.SuggestionCache.TryGetValue(interactionId, out (KenobiAPIV2SearchResult Result, DateTime Timestamp) value2))
                {
                    var result = value2.Result;
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
                        result.Tracks = [result.Tracks[trackIndex]];
                        await PlayDatabaseTracks(player, result, doModifyOriginalResponse: true).ConfigureAwait(false);
                    }
                    else if (selectedValue.StartsWith("album_"))
                    {
                        var albumIndex = int.Parse(selectedValue.Substring(6));
                        result.Tracks.Clear();
                        result.Albums = [result.Albums[albumIndex]];
                        await PlayDatabaseTracks(player, result, doModifyOriginalResponse: true).ConfigureAwait(false);
                    }
                }
                else
                {
                    await FollowupAsync("This suggestion menu has expired. Please try your search again.").ConfigureAwait(false);
                    return;
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