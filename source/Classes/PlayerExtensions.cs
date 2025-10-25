using Lavalink4NET.Players;
using PPMusicBot.Models;
namespace PPMusicBot.Classes;
public static class PlayerExtensions
{
    public static CustomQueueTrackItem? GetCustomData(this ITrackQueueItem item)
    {
        return (item as CustomQueueTrackItem);
    }

    public static bool TryGetCustomData(this LavalinkPlayer player, out CustomQueueTrackItem? data)
    {
        if (player.CurrentItem is CustomQueueTrackItem customItem)
        {
            data = customItem;
            return true;
        }
        data = default;
        return false;
    }
}