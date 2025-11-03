using System.Text;
using Discord.Interactions;
namespace PPMusicBot.Commands.SlashCommands.MusicSlashCommandModule
{
    public sealed partial class MusicSlashCommandModule
    {
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
    }
}
