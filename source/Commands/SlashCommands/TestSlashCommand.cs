using Discord;
using Discord.Interactions;
using System.Threading.Tasks;

namespace PPMusicBot.Commands.SlashCommands
{
    public class TestSlashCommand : InteractionModuleBase<SocketInteractionContext>
    {
        [SlashCommand("test", "A test command.")]
        public async Task Test()
        {
            await RespondAsync($"Hello {Context.User.Username}!");
        }
    }
}