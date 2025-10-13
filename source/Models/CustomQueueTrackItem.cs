using Lavalink4NET.Players;
using Lavalink4NET.Tracks;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PPMusicBot.Models
{
    public class CustomQueueTrackItem(LavalinkTrack track, KenobiAPIModels.MusicTrack musicTrack) : ITrackQueueItem
    {
        public TrackReference Reference => new(track);

        LavalinkTrack? Track => Reference.Track;

        string Identifier => Reference.Identifier!;
        public KenobiAPIModels.MusicTrack MusicTrack => musicTrack;
    }
}
