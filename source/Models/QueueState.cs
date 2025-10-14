using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PPMusicBot.Models
{
    public record QueueState
    {
        public ulong GuildId { get; set; }
        public int CurrentPage { get; set; }
        public DateTime LastUpdated { get; set; }
    }
}
