namespace PPMusicBot.Commands.SlashCommands.MusicSlashCommandModule;
using System;
using System.Text;
using System.Threading.Tasks;
using Discord;
using Discord.Interactions;
using Lavalink4NET.DiscordNet;
using Lavalink4NET.Players;
using Lavalink4NET.Players.Vote;
using Lavalink4NET.Rest.Entities.Tracks;
using Lavalink4NET.Artwork;
using Microsoft.Extensions.Logging;
using PPMusicBot.Helpers;
using PPMusicBot.Models;
using PPMusicBot.Services;
using Lavalink4NET;

/// <summary>
///     Presents some of the main features of the Lavalink4NET-Library.
/// </summary>
[RequireContext(ContextType.Guild)]
public sealed class MusicSlashCommandModule : InteractionModuleBase<SocketInteractionContext>
{
    private readonly IAudioService _audioService;
    private readonly ILogger<MusicSlashCommandModule> _logger;
    private readonly ArtworkService _artworkService;

    private readonly KenobiAPISearchEngineService _kenobiAPISearchEngineService;
    /// <summary>
    ///     Initializes a new instance of the <see cref="MusicModule"/> class.
    /// </summary>
    /// <param name="audioService">the audio service</param>
    /// <exception cref="ArgumentNullException">
    ///     thrown if the specified <paramref name="audioService"/> is <see langword="null"/>.
    /// </exception>
    public MusicSlashCommandModule(IAudioService audioService, ILogger<MusicSlashCommandModule> logger, KenobiAPISearchEngineService kenobiAPISearchEngineService, ArtworkService artworkService)
    {
        ArgumentNullException.ThrowIfNull(audioService);

        _audioService = audioService;
        _logger = logger;
        _artworkService = artworkService;

        _kenobiAPISearchEngineService = kenobiAPISearchEngineService;
    }

    /// <summary>
    ///     Disconnects from the current voice channel connected to asynchronously.
    /// </summary>
    /// <returns>a task that represents the asynchronous operation</returns>
    [SlashCommand("disconnect", "Disconnects from the current voice channel connected to", runMode: RunMode.Async)]
    public async Task DisconnectAsync()
    {
        var player = await GetPlayerAsync().ConfigureAwait(false);

        if (player is null)
        {
            return;
        }

        await player.DisconnectAsync().ConfigureAwait(false);
        await RespondAsync("Disconnected.").ConfigureAwait(false);
    }
    [SlashCommand("play-external", description: "A file link, icecast stream, etca", runMode: RunMode.Async)]
    public async Task PlayExternalAsync(string query)
    {
        try
        {
            await DeferAsync().ConfigureAwait(false);

            var player = await GetPlayerAsync(connectToVoiceChannel: true).ConfigureAwait(false);

            if (player is null)
            {
                await FollowupAsync("Failed to connect to voice channel.").ConfigureAwait(false);
                return;
            }
            // I honestly don't really know how it's supposed to work, so I'll just put it here and hope for the best.
            var track = await _audioService.Tracks.LoadTrackAsync(query, TrackSearchMode.YouTube);

            if (track is null)
            {
                await FollowupAsync("Lavalink could not load the external track.").ConfigureAwait(false);
                return;
            }

            var position = await player.PlayAsync(track).ConfigureAwait(false);

            await FollowupAsync($"Playing [external url]({track.Uri?.OriginalString}).").ConfigureAwait(false);
            return;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, ex.Message);
            throw;
        }
    }
    [SlashCommand("play-yt", description: "A test command for youtube music and such.", runMode: RunMode.Async)]
    public async Task PlayYoutubeAsync(string query)
    {
        try
        {
            await DeferAsync().ConfigureAwait(false);

            var player = await GetPlayerAsync(connectToVoiceChannel: true).ConfigureAwait(false);

            if (player is null)
            {
                await FollowupAsync("Failed to connect to voice channel.").ConfigureAwait(false);
                return;
            }

            try
            {
                var uriFromQuery = new Uri(query);
                if (uriFromQuery.Host == "www.youtube.com" || uriFromQuery.Host == "youtu.be" || uriFromQuery.Host == "music.youtube.com")
                {
                    if (uriFromQuery.Query.Contains("?list="))
                    {
                        var playlist = await _audioService.Tracks.LoadTracksAsync(query, TrackSearchMode.YouTube);

                        if (playlist.Tracks.Length == 0 || !playlist.IsPlaylist)
                        {
                            await FollowupAsync("We thought the result was a playlist, but it's not or it's empty.").ConfigureAwait(false);
                            return;
                        }

                        foreach (var track in playlist.Tracks)
                        {
                            await player.PlayAsync(track).ConfigureAwait(false);
                        }

                        await FollowupAsync($"Added {playlist.Tracks.Length} tracks from {playlist.Playlist?.Name}.").ConfigureAwait(false);
                        return;
                    }
                    else
                    {
                        var track = await _audioService.Tracks.LoadTrackAsync(query, TrackSearchMode.YouTube);

                        if (track is null)
                        {
                            await FollowupAsync("Lavalink could not load the youtube track.").ConfigureAwait(false);
                            return;
                        }

                        var position = await player.PlayAsync(track).ConfigureAwait(false);

                        await FollowupAsync(embed: await Helpers.BuildPlayingEmbed(position, track, null, _artworkService)).ConfigureAwait(false);
                        return;
                    }
                }
                else
                {
                    await FollowupAsync("This does not seem to be a proper youtube url.").ConfigureAwait(false);
                    return;
                }
            }
            catch (UriFormatException)
            {
                await FollowupAsync("Invalid URL format provided.").ConfigureAwait(false);
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
    [SlashCommand("play", description: "Plays music", runMode: RunMode.Async)]
    public async Task PlayAsync(string query)
    {
        try
        {
            await DeferAsync().ConfigureAwait(false);

            var player = await GetPlayerAsync(connectToVoiceChannel: true).ConfigureAwait(false);

            if (player is null)
            {
                return;
            }

            var result = await _kenobiAPISearchEngineService.Search(query, Context.Interaction.Id).ConfigureAwait(false);

            if (result is null)
            {
                await FollowupAsync("The database did not find any tracks.").ConfigureAwait(false);
                return;
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
                        sb.AppendLine($"{result.Tracks[i].title} - Score: {result.Tracks[i].score}");
                        menuBuilder.AddOption($"Track: {result.Tracks[i].title}", $"track_{i}");
                    }
                }

                if (result.Albums.Count > 0 && menuBuilder.Options.Count < 25)
                {
                    sb.AppendLine("**Albums:**");
                    var remainingSlots = 25 - menuBuilder.Options.Count;
                    var albumCount = Math.Min(result.Albums.Count, remainingSlots);
                    for (var i = 0; i < albumCount; i++)
                    {
                        sb.AppendLine($"{result.Albums[i].name} - Score: {result.Albums[i].score}");
                        menuBuilder.AddOption($"Album: {result.Albums[i].name}", $"album_{i}");
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
            // Uhh... subcommands... right... so, I already made a working embed creator for tracks, so it should work with pretty much anything now.
            // I would need a file subcommand, a youtube subcommand and fromdb subcommand. (curretnly this is basically fromdb)

            // After I make the suggestion display handler, I should make a separate subcommand for database, youtube and external tracks, fairly sure Lavalink
            // Should handle those pretty well itself, we'll see though.
            await PlayDatabaseTracks(player, result);
        }
        catch (Exception ex)
        {
            _logger.LogError($"{ex.Message} {ex.StackTrace}");
            throw;
        }
    }

    [ComponentInteraction("suggestion_selector:*")]
    public async Task HandleSuggestionSelection(ulong interactionId, string[] selectedOptions)
    {
        try
        {
            await DeferAsync().ConfigureAwait(false);
            if (!_kenobiAPISearchEngineService.SuggestionCache.ContainsKey(interactionId)) {
                await FollowupAsync("This suggestion menu has expired. Please try your search again.").ConfigureAwait(false);
                return;
            }
            var result = _kenobiAPISearchEngineService.SuggestionCache[interactionId].Result;
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
                await PlayDatabaseTracks(player, result, wantedAlbumIndex:  albumIndex, doModifyOriginalResponse: true);
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

    /// <summary>
    ///     Stops the current track asynchronously.
    /// </summary>
    /// <returns>a task that represents the asynchronous operation</returns>
    [SlashCommand("stop", description: "Stops the current track", runMode: RunMode.Async)]
    public async Task StopAsync()
    {
        var player = await GetPlayerAsync(connectToVoiceChannel: false);

        if (player is null)
        {
            return;
        }

        if (player.CurrentItem is null)
        {
            await RespondAsync("Nothing playing!").ConfigureAwait(false);
            return;
        }

        await player.StopAsync().ConfigureAwait(false);
        await RespondAsync("Stopped playing.").ConfigureAwait(false);
    }

    /// <summary>
    ///     Updates the player volume asynchronously.
    /// </summary>
    /// <param name="volume">the volume (1 - 1000)</param>
    /// <returns>a task that represents the asynchronous operation</returns>
    [SlashCommand("volume", description: "Sets the player volume (0 - 200%)", runMode: RunMode.Async)]
    public async Task Volume(int volume = 100)
    {
        if (volume is > 200 or < 0)
        {
            await RespondAsync("Volume out of range: 0% - 200%!").ConfigureAwait(false);
            return;
        }

        var player = await GetPlayerAsync(connectToVoiceChannel: false).ConfigureAwait(false);

        if (player is null)
        {
            return;
        }

        await player.SetVolumeAsync(volume / 100f).ConfigureAwait(false);
        await RespondAsync($"Volume updated: {volume}%").ConfigureAwait(false);
    }

    [SlashCommand("skip", description: "Skips the current track", runMode: RunMode.Async)]
    public async Task SkipAsync()
    {
        var player = await GetPlayerAsync(connectToVoiceChannel: false);

        if (player is null)
        {
            return;
        }

        if (player.CurrentItem is null)
        {
            await RespondAsync("Nothing playing!", ephemeral: true).ConfigureAwait(false);
            return;
        }

        await player.SkipAsync().ConfigureAwait(false);

        var track = player.CurrentItem;

        if (track is not null)
        {
            var embed = new EmbedBuilder()
            {
                Title = "Skipped.",
                Description = $"Now Playing: {track.Reference.Track?.Title}"
            }.Build();
            await RespondAsync(embed: embed).ConfigureAwait(false);
        }
        else
        {
            await RespondAsync("Skipped. Stopped playing because the queue is now empty.").ConfigureAwait(false);
        }
    }

    [SlashCommand("pause", description: "Pauses the player.", runMode: RunMode.Async)]
    public async Task PauseAsync()
    {
        var player = await GetPlayerAsync(connectToVoiceChannel: false);

        if (player is null)
        {
            return;
        }

        if (player.State is PlayerState.Paused)
        {
            await RespondAsync("Player is already paused.").ConfigureAwait(false);
            return;
        }

        await player.PauseAsync().ConfigureAwait(false);
        await RespondAsync("Paused.").ConfigureAwait(false);
    }

    [SlashCommand("resume", description: "Resumes the player.", runMode: RunMode.Async)]
    public async Task ResumeAsync()
    {
        var player = await GetPlayerAsync(connectToVoiceChannel: false);

        if (player is null)
        {
            return;
        }

        if (player.State is not PlayerState.Paused)
        {
            await RespondAsync("Player is not paused.").ConfigureAwait(false);
            return;
        }

        await player.ResumeAsync().ConfigureAwait(false);
        await RespondAsync("Resumed.").ConfigureAwait(false);
    }

    [SlashCommand("np", description: "Displays the currently playing track.", runMode: RunMode.Async)]
    public async Task NowPlayingAsync()
    {
        try
        {
            var player = await GetPlayerAsync(connectToVoiceChannel: false);

            if (player is null)
            {
                return;
            }

            if (player.CurrentItem is null)
            {
                await RespondAsync("Nothing playing!", ephemeral: true).ConfigureAwait(false);
                return;
            }

            var track = player.CurrentItem;

            if (track is not null)
            {
                await RespondAsync(embed: await Helpers.BuildCurrentlyPlayingEmbed(track, player, _artworkService), ephemeral: true).ConfigureAwait(false);
            }
            else
            {
                await RespondAsync("Current track is nothing? Report this to the developer.").ConfigureAwait(false);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, ex.Message);
            throw;
        }
    }

    [SlashCommand("shuffle", description: "Shuffles the queue randomly.", runMode: RunMode.Async)]
    public async Task ShuffleAsync()
    {
        var player = await GetPlayerAsync(connectToVoiceChannel: false);

        if (player is null)
        {
            return;
        }

        if (player.Queue.Count is 0)
        {
            await RespondAsync("The queue is empty!", ephemeral: true).ConfigureAwait(false);
            return;
        }

        await player.Queue.ShuffleAsync();
        await RespondAsync("The queue was shuffled!", ephemeral: false).ConfigureAwait(false);
    }

    [SlashCommand("move", description: "Moves a track to a new position.", runMode: RunMode.Async)]
    public async Task MoveAsync(int trackToMove, int position)
    {
        var player = await GetPlayerAsync(connectToVoiceChannel: false);

        if (player is null)
        {
            return;
        }

        if (player.Queue.Count is 0)
        {
            await RespondAsync("The queue is empty!", ephemeral: true).ConfigureAwait(false);
            return;
        }

        if (trackToMove - 1 > player.Queue.Count || trackToMove - 1 < 0)
        {
            await RespondAsync("There is no track on that position.", ephemeral: true).ConfigureAwait(false);
            return;
        }
        var item = player.Queue[trackToMove - 1];
        if (position > player.Queue.Count)
        {
            // move the track to the end of the queue
            await player.Queue.RemoveAtAsync(trackToMove - 1);
            await player.Queue.AddAsync(item);
            await RespondAsync("Moved the track to the end of the queue.").ConfigureAwait(false);
            return;
        }
        else if (position - 1 <= 0)
        {
            // move the track to the first position
            await player.Queue.RemoveAtAsync(trackToMove - 1);
            await player.Queue.InsertAsync(0, item);
            await RespondAsync("Moved the track to the start of the queue.").ConfigureAwait(false);
            return;
        }
        else
        {
            await player.Queue.RemoveAtAsync(trackToMove - 1);
            await player.Queue.InsertAsync(position - 1, item);
            await RespondAsync($"Inserted the track to position number {position}.").ConfigureAwait(false);
            return;
        }
    }

    [SlashCommand("remove", description: "Remove a track or a range of tracks from the queue.", runMode: RunMode.Async)]
    public async Task RemoveAsync(int position1, int? position2 = null)
    {
        var player = await GetPlayerAsync(connectToVoiceChannel: false);

        if (player is null)
        {
            return;
        }

        if (player.Queue.Count is 0)
        {
            await RespondAsync("The queue is empty!", ephemeral: true).ConfigureAwait(false);
            return;
        }

        if (position1 - 1 > player.Queue.Count || position1 - 1 < 0)
        {
            await RespondAsync($"There is nothing on position {position1}!", ephemeral: true).ConfigureAwait(false);
            return;
        }
        if (position1 > player.Queue.Count || position1 - 1 < 0)
        {
            await RespondAsync("The first position is larger than the queue or less than one.").ConfigureAwait(false);
            return;
        }

        if (position2 is null)
        {
            await player.Queue.RemoveAtAsync(position1 - 1);
            await RespondAsync($"Removed the track at position {position1}").ConfigureAwait(false);
            return;
        }
        else if (position2 is not null) 
        {
            int startPos = Math.Clamp(position1, 1, player.Queue.Count);
            int endPos = Math.Clamp((int)position2, 1, player.Queue.Count);

            if (startPos > endPos)
            {
                (startPos, endPos) = (endPos, startPos);
            }

            int startIndex = startPos - 1;
            int amountToRemove = endPos - startPos + 1;

            amountToRemove = Math.Clamp(amountToRemove, 0, player.Queue.Count - startIndex);

            await player.Queue.RemoveRangeAsync(startIndex, amountToRemove);
            await RespondAsync($"Removed tracks from position {startPos} to {endPos}. (removed {amountToRemove} tracks)").ConfigureAwait(false);
        }
    }

    [SlashCommand("queue", description: "Shows the tracks in the queue.", runMode: RunMode.Async)]
    public async Task QueueAsync(int page = 0)
    {
        var player = await GetPlayerAsync(connectToVoiceChannel: false);

        if (player is null)
        {
            return;
        }

        if (player.Queue.IsEmpty)
        {
            await RespondAsync("The queue is empty.", ephemeral: true).ConfigureAwait(false);
            return;
        }

        // queue state for pagination
        var queueState = new QueueState
        {
            GuildId = Context.Guild.Id,
            CurrentPage = page,
            LastUpdated = DateTime.UtcNow
        };

        var (embed, components) = Helpers.BuildQueueEmbed(player, queueState.CurrentPage);

        if (embed.Description.Length > 4096) // I think the limit was 4096??  Well it didn't crash yet.
        {
            await RespondAsync("The queue is too long to display.").ConfigureAwait(false);
            return;
        }

        await RespondAsync(embed: embed, components: components).ConfigureAwait(false);
    }

    // I am not sure if these should be here tbh
    // Does this warrant creation of a separate file for the queue command?
    [ComponentInteraction("queue_prev")]
    public async Task HandleQueuePrevious()
    {
        await HandleQueuePagination(-1);
    }

    [ComponentInteraction("queue_next")]
    public async Task HandleQueueNext()
    {
        await HandleQueuePagination(1);
    }

    private async Task HandleQueuePagination(int direction)
    {
        await DeferAsync().ConfigureAwait(false);

        var player = await GetPlayerAsync(connectToVoiceChannel: false);
        if (player is null || player.Queue.IsEmpty)
        {
            await ModifyOriginalResponseAsync(msg =>
            {
                msg.Content = "Queue is no longer available.";
                msg.Components = new ComponentBuilder().Build();
            }).ConfigureAwait(false);
            return;
        }

        var originalMessage = await GetOriginalResponseAsync();
        if (originalMessage.Embeds.Count == 0)
            throw new ArgumentNullException(nameof(originalMessage.Embeds));
        var currentPage = ExtractCurrentPage(originalMessage.Embeds.FirstOrDefault()!);

        var newPage = currentPage + direction;
        var (embed, components) = Helpers.BuildQueueEmbed(player, newPage);

        await ModifyOriginalResponseAsync(msg =>
        {
            msg.Embed = embed;
            msg.Components = components;
        }).ConfigureAwait(false);
    }

    private int ExtractCurrentPage(IEmbed embed)
    {
        if (embed?.Title == null) return 0;

        var titleParts = embed.Title.Split(" - Page ");
        if (titleParts.Length == 2)
        {
            var pageParts = titleParts[1].Split('/');
            if (pageParts.Length == 2 && int.TryParse(pageParts[0], out int currentPage))
            {
                return currentPage - 1; // Convert to 0-based index
            }
        }
        return 0;
    }

    [SlashCommand("debug", "Debug player state", runMode: RunMode.Async)]
    public async Task DebugPlayer()
    {
        var player = await GetPlayerAsync(connectToVoiceChannel: false);
        if (player is null) return;

        var debugInfo = new StringBuilder();
        debugInfo.AppendLine($"Current Track: {player.CurrentTrack?.Title}");
        debugInfo.AppendLine($"Queue Count: {player.Queue.Count}");
        debugInfo.AppendLine($"Player State: {player.State}");
        debugInfo.AppendLine($"Repeat Mode: {player.RepeatMode}");

        for (int i = 0; i < Math.Min(player.Queue.Count, 5); i++)
        {
            debugInfo.AppendLine($"Queue[{i}]: {player.Queue[i].Track?.Title}");
        }

        await RespondAsync(debugInfo.ToString());
    }

    /// <summary>
    ///     Gets the guild player asynchronously.
    /// </summary>
    /// <param name="connectToVoiceChannel">
    ///     a value indicating whether to connect to a voice channel
    /// </param>
    /// <returns>
    ///     a task that represents the asynchronous operation. The task result is the lavalink player.
    /// </returns>
    private async ValueTask<VoteLavalinkPlayer?> GetPlayerAsync(bool connectToVoiceChannel = true)
    {
        var retrieveOptions = new PlayerRetrieveOptions(
            ChannelBehavior: connectToVoiceChannel ? PlayerChannelBehavior.Join : PlayerChannelBehavior.None);

        var result = await _audioService.Players
            .RetrieveAsync(Context, playerFactory: PlayerFactory.Vote, retrieveOptions)
            .ConfigureAwait(false);

        if (!result.IsSuccess)
        {
            var errorMessage = result.Status switch
            {
                PlayerRetrieveStatus.UserNotInVoiceChannel => "You are not connected to a voice channel.",
                PlayerRetrieveStatus.BotNotConnected => "The bot is currently not connected.",
                _ => "Unknown error.",
            };

            await FollowupAsync(errorMessage).ConfigureAwait(false);
            return null;
        }

        return result.Player;
    }

    private async Task PlayDatabaseTracks(VoteLavalinkPlayer player, KenobiAPISearchResult result, int wantedTrackIndex = 0, int wantedAlbumIndex = 0, bool doModifyOriginalResponse = false)
    {
        if (result.Tracks.Count > 0 && result.Albums.Count == 0)
        {
            var track = await _audioService.Tracks.LoadTrackAsync(_kenobiAPISearchEngineService.GetTrackUriFromTrackObject(result.Tracks[wantedTrackIndex]).OriginalString, TrackSearchMode.None);

            if (track is null)
            {
                await FollowupAsync("Lavalink could not load the track.");
                return;
            }
            result.Tracks = [result.Tracks[wantedTrackIndex]];
            var position = await player.PlayAsync(new CustomQueueTrackItem(track, result.Tracks[0])).ConfigureAwait(false);

            if (!doModifyOriginalResponse) await FollowupAsync(embed: await Helpers.BuildPlayingEmbed(position, track, result, null)).ConfigureAwait(false);
            else await ModifyOriginalResponseAsync(async msg =>
            {
                msg.Embed = await Helpers.BuildPlayingEmbed(position, track, result, null);
                msg.Components = new ComponentBuilder().Build();
            }).ConfigureAwait(false);
        }
        else if (result.Albums.Count > 0)
        {
            if (wantedAlbumIndex != 0)
            {
                result.Albums = [result.Albums[wantedAlbumIndex]];
            }
            if (result.Albums[0].Music.Count == 0)
            {
                result.Albums[0].Music = await _kenobiAPISearchEngineService.RequestAlbumSongsAsync(result.Albums[0].id);
            }
            var firstToPlay = await _audioService.Tracks.LoadTrackAsync(_kenobiAPISearchEngineService.GetTrackUriFromTrackObject(result.Albums[0].Music[0]).OriginalString, TrackSearchMode.None);
            if (firstToPlay is null)
            {
                await FollowupAsync("Huh? The first track in the sequence is not available? Aborting.");
                return;
            }
            var position = await player.PlayAsync(new CustomQueueTrackItem(firstToPlay, result.Albums[0].Music[0])).ConfigureAwait(false);

            if (!doModifyOriginalResponse) await FollowupAsync(embed: await Helpers.BuildPlayingEmbed(position, null, result, null)).ConfigureAwait(false);
            else await ModifyOriginalResponseAsync(async msg =>
            {
                msg.Embed = await Helpers.BuildPlayingEmbed(position, null, result, null);
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
            throw new ArgumentNullException(nameof(result));
        }
    }
}