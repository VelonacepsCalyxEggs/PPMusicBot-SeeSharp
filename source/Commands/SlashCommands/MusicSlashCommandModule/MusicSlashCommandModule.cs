using Discord.Interactions;
using Lavalink4NET.DiscordNet;
using Lavalink4NET.Players;
using Lavalink4NET.Players.Vote;
using Lavalink4NET.Artwork;
using PPMusicBot.Services;
using Lavalink4NET;

namespace PPMusicBot.Commands.SlashCommands.MusicSlashCommandModule;
/// <summary>
///     Presents some of the main features of the Lavalink4NET-Library.
/// </summary>
[RequireContext(ContextType.Guild)]
//[Group("music", "Music commands")]
public sealed partial class MusicSlashCommandModule : InteractionModuleBase<SocketInteractionContext>
{
    private readonly IAudioService _audioService;
    private readonly ILogger<MusicSlashCommandModule> _logger;
    private readonly ArtworkService _artworkService;
    private readonly MusicService _musicService;

    private readonly KenobiAPISearchEngineService _kenobiAPISearchEngineService;
    /// <summary>
    ///     Initializes a new instance of the <see cref="MusicModule"/> class.
    /// </summary>
    /// <param name="audioService">the audio service</param>
    /// <exception cref="ArgumentNullException">
    ///     thrown if the specified <paramref name="audioService"/> is <see langword="null"/>.
    /// </exception>
    public MusicSlashCommandModule(
        IAudioService audioService, 
        ILogger<MusicSlashCommandModule> logger, 
        KenobiAPISearchEngineService kenobiAPISearchEngineService, 
        ArtworkService artworkService,
        MusicService musicService)
    {
        _audioService = audioService;
        _logger = logger;
        _artworkService = artworkService;
        _kenobiAPISearchEngineService = kenobiAPISearchEngineService;
        _musicService = musicService;
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
    /// 
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
        _musicService.SetTextChannelId(Context.Guild.Id, Context.Channel.Id); // Set interaction channel. (For error and service messages)
        return result.Player;
    }
}