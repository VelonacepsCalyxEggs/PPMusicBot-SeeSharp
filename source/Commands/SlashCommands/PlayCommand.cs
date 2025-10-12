namespace Lavalink4NET.Discord_NET.ExampleBot;

using System;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading.Tasks;
using Discord.Interactions;
using Lavalink4NET.Clients;
using Lavalink4NET.DiscordNet;
using Lavalink4NET.Extensions;
using Lavalink4NET.Players;
using Lavalink4NET.Players.Vote;
using Lavalink4NET.Rest.Entities.Tracks;
using PPMusicBot.Classes;
using PPMusicBot.Models;
using PPMusicBot.Services;
using static PPMusicBot.Models.KenobiAPIModels;

/// <summary>
///     Presents some of the main features of the Lavalink4NET-Library.
/// </summary>
[RequireContext(ContextType.Guild)]
public sealed class MusicSlashCommandModule : InteractionModuleBase<SocketInteractionContext>
{
    private readonly IAudioService _audioService;
    private readonly ILogger<MusicSlashCommandModule> _logger;

    private readonly KenobiAPISearchEngineService _kenobiAPISearchEngineService;

    /// <summary>
    ///     Initializes a new instance of the <see cref="MusicModule"/> class.
    /// </summary>
    /// <param name="audioService">the audio service</param>
    /// <exception cref="ArgumentNullException">
    ///     thrown if the specified <paramref name="audioService"/> is <see langword="null"/>.
    /// </exception>
    public MusicSlashCommandModule(IAudioService audioService, ILogger<MusicSlashCommandModule> logger, KenobiAPISearchEngineService kenobiAPISearchEngineService)
    {
        ArgumentNullException.ThrowIfNull(audioService);

        _audioService = audioService;
        _logger = logger;

        _kenobiAPISearchEngineService = kenobiAPISearchEngineService;
    }

    /// <summary>
    ///     Disconnects from the current voice channel connected to asynchronously.
    /// </summary>
    /// <returns>a task that represents the asynchronous operation</returns>
    [SlashCommand("disconnect", "Disconnects from the current voice channel connected to", runMode: RunMode.Async)]
    public async Task Disconnect()
    {
        var player = await GetPlayerAsync().ConfigureAwait(false);

        if (player is null)
        {
            return;
        }

        await player.DisconnectAsync().ConfigureAwait(false);
        await RespondAsync("Disconnected.").ConfigureAwait(false);
    }

    /// <summary>
    ///     Plays music asynchronously.
    /// </summary>
    /// <param name="query">the search query</param>
    /// <returns>a task that represents the asynchronous operation</returns>
    [SlashCommand("play", description: "Plays music", runMode: RunMode.Async)]
    public async Task Play(string query)
    {
        await DeferAsync().ConfigureAwait(false);

        var player = await GetPlayerAsync(connectToVoiceChannel: true).ConfigureAwait(false);

        if (player is null)
        {
            return;
        }

        var result = await _kenobiAPISearchEngineService.Search(query).ConfigureAwait(false);

        if (result is null)
        {
            await FollowupAsync("The database did not find any tracks.").ConfigureAwait(false);
            return;
        }
        // Next step would probably be a proper embed creator and subcommands. I love how it works so far... also I need to not
        // forget about player event listeners...
        if (result.Tracks.Count > 1)
        {
            var firstToPlay = await _audioService.Tracks.LoadTrackAsync(_kenobiAPISearchEngineService.GetTrackUriFromTrackObject(result.Tracks[0]).OriginalString, TrackSearchMode.None);
            if (firstToPlay is null)
            {
                await FollowupAsync("Huh? The first track in the sequence is not available? Aborting.");
                return;
            }
            var position = await player.PlayAsync(new CustomQueueTrackItem(firstToPlay, result.Tracks[0])).ConfigureAwait(false);
            if (position is 0)
            {
                await FollowupAsync($"Playing: **{firstToPlay.Title}** by **{firstToPlay.Author}** and another {result.Tracks.Count - 1} tracks with it because why the hell not.").ConfigureAwait(false);
            }
            else
            {
                await FollowupAsync($"Added to queue: **{firstToPlay.Title}** by **{firstToPlay.Author}** and another {result.Tracks.Count - 1} tracks with it because why the hell not.").ConfigureAwait(false);
            }
            foreach (var resultTrack in result.Tracks[1..])
            {
                var track = await _audioService.Tracks.LoadTrackAsync(_kenobiAPISearchEngineService.GetTrackUriFromTrackObject(resultTrack).OriginalString, TrackSearchMode.None);
                if (track is null) { continue; }
                await player.PlayAsync(new CustomQueueTrackItem(track, resultTrack)).ConfigureAwait(false);
            }
        }
        else
        {
            var track = await _audioService.Tracks.LoadTrackAsync(_kenobiAPISearchEngineService.GetTrackUriFromTrackObject(result.Tracks[0]).OriginalString, TrackSearchMode.None);
            if (track is null)
            {
                await FollowupAsync("Lavalink could not load the track.");
                return;
            }

            var position = await player.PlayAsync(new CustomQueueTrackItem(track, result.Tracks[0])).ConfigureAwait(false);

            if (position is 0)
            {
                await FollowupAsync($"Playing: **{track.Title}** by **{track.Author}**").ConfigureAwait(false);
            }
            else
            {
                await FollowupAsync($"Added to queue: **{track.Title}** by **{track.Author}**").ConfigureAwait(false);
            }
        }
    }

    /// <summary>
    ///     Shows the track position asynchronously.
    /// </summary>
    /// <returns>a task that represents the asynchronous operation</returns>
    [SlashCommand("position", description: "Shows the track position", runMode: RunMode.Async)]
    public async Task Position()
    {
        var player = await GetPlayerAsync(connectToVoiceChannel: false).ConfigureAwait(false);

        if (player is null)
        {
            return;
        }

        if (player.CurrentItem is null)
        {
            await RespondAsync("Nothing playing!").ConfigureAwait(false);
            return;
        }

        var customData = PlayerExtensions.GetCustomData(player);
        await RespondAsync($"Position: {player.Position?.Position} / {player.CurrentTrack?.Duration} {customData?.title ?? player.CurrentTrack?.Title}.").ConfigureAwait(false);
    }

    /// <summary>
    ///     Stops the current track asynchronously.
    /// </summary>
    /// <returns>a task that represents the asynchronous operation</returns>
    [SlashCommand("stop", description: "Stops the current track", runMode: RunMode.Async)]
    public async Task Stop()
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
    [SlashCommand("volume", description: "Sets the player volume (0 - 1000%)", runMode: RunMode.Async)]
    public async Task Volume(int volume = 100)
    {
        if (volume is > 1000 or < 0)
        {
            await RespondAsync("Volume out of range: 0% - 1000%!").ConfigureAwait(false);
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
    public async Task Skip()
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

        await player.SkipAsync().ConfigureAwait(false);

        var track = player.CurrentItem;

        if (track is not null)
        {
            await RespondAsync($"Skipped. Now playing: {track.Track!.Uri}").ConfigureAwait(false);
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
}