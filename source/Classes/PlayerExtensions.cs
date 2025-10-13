using Lavalink4NET.Players;
using PPMusicBot.Models;
using System.Numerics;
namespace PPMusicBot.Classes;
public static class PlayerExtensions
{
    public static KenobiAPIModels.MusicTrack? GetCustomData(this ITrackQueueItem item)
    {
        return (item as CustomQueueTrackItem)?.MusicTrack;
    }

    public static bool TryGetCustomData(this LavalinkPlayer player, out KenobiAPIModels.MusicTrack? data)
    {
        if (player.CurrentItem is CustomQueueTrackItem customItem)
        {
            data = customItem.MusicTrack;
            return true;
        }
        data = default;
        return false;
    }
}