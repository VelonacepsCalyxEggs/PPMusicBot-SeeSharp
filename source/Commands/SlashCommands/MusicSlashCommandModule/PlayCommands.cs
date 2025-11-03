using System.Text;
using Discord;
using Discord.Interactions;
using Lavalink4NET.Players.Vote;
using Lavalink4NET.Rest.Entities.Tracks;
using PPMusicBot.Models;
using PPMusicBot.Services;
using static PPMusicBot.Helpers.Helpers;
namespace PPMusicBot.Commands.SlashCommands.MusicSlashCommandModule
{
    public sealed partial class MusicSlashCommandModule
    {

        [SlashCommand("play-attachment", description: "A file attachment, only mp3,mp4,wav,ogg", runMode: RunMode.Async)]
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
                    var track = await _audioService.Tracks.LoadTrackAsync(file.Url, TrackSearchMode.None);

                    if (track is null)
                    {
                        await FollowupAsync("Lavalink could not load the attachment.").ConfigureAwait(false);
                        return;
                    }

                    var position = await player.PlayAsync(track).ConfigureAwait(false);

                    await FollowupAsync(embed: await BuildPlayingEmbed(position, track, null, _artworkService)).ConfigureAwait(false);
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
        [SlashCommand("play-external", description: "A file link, icecast stream, etc", runMode: RunMode.Async)]
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
                var track = await _audioService.Tracks.LoadTrackAsync(query, TrackSearchMode.None);

                if (track is null)
                {
                    await FollowupAsync("Lavalink could not load the external track.").ConfigureAwait(false);
                    return;
                }

                var position = await player.PlayAsync(track).ConfigureAwait(false);

                await FollowupAsync(embed: await BuildPlayingEmbed(position, track, null, _artworkService)).ConfigureAwait(false);
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

                            await FollowupAsync(embed: await BuildPlayingEmbed(position, track, null, _artworkService)).ConfigureAwait(false);
                            return;
                        }
                    }
                    else
                    {
                        await FollowupAsync("This does not seem to be a YouTube URL.").ConfigureAwait(false);
                        return;
                    }
                }
                catch (UriFormatException)
                {
                    var track = await _audioService.Tracks.LoadTrackAsync(query, TrackSearchMode.YouTube).ConfigureAwait(false);

                    if (track is null)
                    {
                        await FollowupAsync("Lavalink could not find or load any YouTube tracks with your query.").ConfigureAwait(false);
                        return;
                    }
                    var position = await player.PlayAsync(track).ConfigureAwait(false);

                    await FollowupAsync(embed: await BuildPlayingEmbed(position, track, null, _artworkService)).ConfigureAwait(false);
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

                _musicService.SetTextChannelId(Context.Guild.Id, Context.Channel.Id); // Set interaction channel. (For error and service messages)

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

                if (!doModifyOriginalResponse) await FollowupAsync(embed: await BuildPlayingEmbed(position, track, result, null)).ConfigureAwait(false);
                else await ModifyOriginalResponseAsync(async msg =>
                {
                    msg.Embed = await BuildPlayingEmbed(position, track, result, null);
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
                throw new ArgumentNullException(nameof(result));
            }
        }

        [ComponentInteraction("suggestion_selector:*")]
        public async Task HandleSuggestionSelection(ulong interactionId, string[] selectedOptions)
        {
            try
            {
                await DeferAsync().ConfigureAwait(false);
                if (!_kenobiAPISearchEngineService.SuggestionCache.ContainsKey(interactionId))
                {
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
    }
}
