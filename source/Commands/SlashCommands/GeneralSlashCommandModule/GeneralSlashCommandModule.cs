using Discord;
using Discord.Interactions;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Text;

namespace PPMusicBot.Commands.SlashCommands.GeneralSlashCommandModule
{
    public partial class GeneralSlashCommandModule(ILogger<GeneralSlashCommandModule> logger) : InteractionModuleBase<SocketInteractionContext>
    {
        private readonly ILogger<GeneralSlashCommandModule> _logger = logger;
        private readonly string[] _hostsToPing = ["google.com", "yandex.ru", "funckenobi42.space"];
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
        [SlashCommand("ping", description: "Pings all of related services to funckenobi42.space, made for testing during government shutdowns.", runMode: RunMode.Async)]
        public async Task PingServicesAsync()
        {
            await DeferAsync().ConfigureAwait(false);
            StringBuilder sb = new StringBuilder();

            foreach (string host in _hostsToPing)
            {
                using var ping = new Ping();
                try
                {
                    IPAddress[] addresses = await Dns.GetHostAddressesAsync(host).ConfigureAwait(false);
                    IPAddress ipv4 = addresses.FirstOrDefault(a => a.AddressFamily == AddressFamily.InterNetwork)!;

                    if (ipv4 == null)
                    {
                        sb.AppendLine($"{host}: Could not resolve IPv4 address.");
                        continue;
                    }

                    PingReply reply = await ping.SendPingAsync(ipv4, 2000).ConfigureAwait(false);

                    if (reply.Status == IPStatus.Success)
                    {
                        sb.AppendLine($"{host} ({ipv4}): {reply.RoundtripTime}ms");
                    }
                    else
                    {
                        sb.AppendLine($"{host} ({ipv4}): {reply.Status}");
                    }
                }
                catch (Exception e)
                {
                    string errorMsg = e.InnerException?.Message ?? e.Message;
                    sb.AppendLine($"{host}: Error - {errorMsg}");
                    _logger.LogError(e, $"Ping failed for {host}");
                }
            }
            await FollowupAsync(sb.ToString()).ConfigureAwait(false);
        }

    }
}
