using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PPMusicBot.Services
{
    /// <summary>
    ///     Mostly used to store context for the player.
    ///     Will probably get expanded later.
    /// </summary>
    public class MusicService
    {
        private readonly ConcurrentDictionary<ulong, ulong> _guildTextChannels = new();
        private readonly ILogger<MusicService> _logger;
        private readonly IConfiguration _configuration;

        public MusicService(ILogger<MusicService> logger, IConfiguration configuration)
        {
            _logger = logger;
            _configuration = configuration;
        }

        public void SetTextChannelId(ulong guildId, ulong channel)
        {
            _guildTextChannels[guildId] = channel;
        }
        public ulong? GetTextChannelId(ulong guildId)
        {
            if (_guildTextChannels.TryGetValue(guildId, out var channel)) return channel;
            else return null;
        }
        public void ClearTextChannel(ulong guildId)
        {
            _guildTextChannels.Remove(guildId, out _);
        }
    }
}
