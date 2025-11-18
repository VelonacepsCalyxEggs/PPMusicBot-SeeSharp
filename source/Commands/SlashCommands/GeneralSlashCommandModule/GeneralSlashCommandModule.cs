using Discord;
using Discord.Interactions;
using System.Text;

namespace PPMusicBot.Commands.SlashCommands.GeneralSlashCommandModule
{
    public partial class GeneralSlashCommandModule(ILogger<GeneralSlashCommandModule> logger) : InteractionModuleBase<SocketInteractionContext>
    {
        private readonly ILogger<GeneralSlashCommandModule> _logger = logger;
        [SlashCommand("whereami", description: "Retrieves the list of guilds where the bot resides.", runMode: RunMode.Async)]
        public async Task WhereAmIAsync()
        {
            try
            {
                await DeferAsync().ConfigureAwait(false);
                StringBuilder sb = new StringBuilder();
                int counter = 0;
                foreach (var guild in Context.Client.Guilds)
                {
                    counter++;
                    sb.Append($"{counter}. {guild.Name} - Owner: <@{guild.OwnerId}>\n");
                }
                Embed embed = new EmbedBuilder()
                {
                    Title = "Guilds I am in:",
                    Description = sb.ToString(),
                    Footer = new EmbedFooterBuilder() { Text = $"I am in {counter} guilds." }
                }.Build();

                await FollowupAsync(embed: embed, ephemeral: true).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, ex.StackTrace);
            }
        }
    }
}
