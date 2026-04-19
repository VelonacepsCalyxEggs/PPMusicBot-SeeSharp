using Discord;
using Discord.Interactions;
using Discord.WebSocket;
using Lavalink4NET.Clients;
using Lavalink4NET.Players;
using PPMusicBot.Classes;
using System.Diagnostics;
using System.Text;
using static PPMusicBot.Helpers.Helpers;
namespace PPMusicBot.Commands.SlashCommands.MusicSlashCommandModule
{
    public sealed partial class MusicSlashCommandModule
    {
        /// <summary>
        ///     Disconnects from the current voice channel connected to asynchronously.
        /// </summary>
        /// <returns>a task that represents the asynchronous operation</returns>
        [SlashCommand("disconnect", "Disconnects from the current voice channel connected to", runMode: RunMode.Async)]
        public async Task DisconnectAsync()
        {
            var player = await GetPlayerAsync().ConfigureAwait(false);

            if (player is null)
            {
                return;
            }

            await player.DisconnectAsync().ConfigureAwait(false);
            await RespondAsync("Disconnected.").ConfigureAwait(false);
        }

        /// <summary>
        ///     Stops the current track asynchronously.
        /// </summary>
        /// <returns>a task that represents the asynchronous operation</returns>
        [SlashCommand("stop", description: "Stops the current track", runMode: RunMode.Async)]
        public async Task StopAsync()
        {
            var player = await GetPlayerAsync(connectToVoiceChannel: false);

            if (player is null)
            {
                return;
            }

            if (player.CurrentItem is null)
            {
                await RespondAsync("Nothing playing!").ConfigureAwait(false);
                return;
            }

            await player.StopAsync().ConfigureAwait(false);
            await RespondAsync("Stopped playing.").ConfigureAwait(false);
        }

        /// <summary>
        ///     Updates the player volume asynchronously.
        /// </summary>
        /// <param name="volume">the volume (1 - 1000)</param>
        /// <returns>a task that represents the asynchronous operation</returns>
        [SlashCommand("volume", description: "Sets the player volume (0 - 200%)", runMode: RunMode.Async)]
        public async Task Volume(int volume = 100)
        {
            if (volume is > 200 or < 0)
            {
                await RespondAsync("Volume out of range: 0% - 200%!").ConfigureAwait(false);
                return;
            }

            var player = await GetPlayerAsync(connectToVoiceChannel: false).ConfigureAwait(false);

            if (player is null)
            {
                return;
            }

            await player.SetVolumeAsync(volume / 100f).ConfigureAwait(false);
            await RespondAsync($"Volume updated: {volume}%").ConfigureAwait(false);
        }

        [SlashCommand("skip", description: "Skips the current track", runMode: RunMode.Async)]
        public async Task SkipAsync()
        {
            var player = await GetPlayerAsync(connectToVoiceChannel: false);

            if (player is null)
            {
                return;
            }

            if (player.CurrentItem is null)
            {
                await RespondAsync("Nothing playing!", ephemeral: true).ConfigureAwait(false);
                return;
            }

            await player.SkipAsync().ConfigureAwait(false);
            var currentItem = player.CurrentItem;
            if (currentItem is not null)
            {
                string description = string.Empty;
                if (player.TryGetCustomData(out var data) && data is not null)
                {
                    description = $"Now Playing: {data.MusicTrack.Title}";
                }
                else
                {
                    description = $"Now Playing: {player.CurrentTrack?.Title}";
                }
                var embed = new EmbedBuilder()
                {
                    Title = "Skipped.",
                    Description = description
                }.Build();
                await RespondAsync(embed: embed).ConfigureAwait(false);
            }
            else
            {
                await RespondAsync("Skipped. Stopped playing because the queue is now empty.").ConfigureAwait(false);
            }
        }

        [SlashCommand("pause", description: "Pauses the player.", runMode: RunMode.Async)]
        public async Task PauseAsync()
        {
            var player = await GetPlayerAsync(connectToVoiceChannel: false);

            if (player is null)
            {
                return;
            }

            if (player.State is PlayerState.Paused)
            {
                await RespondAsync("Player is already paused.").ConfigureAwait(false);
                return;
            }

            await player.PauseAsync().ConfigureAwait(false);
            await RespondAsync("Paused.").ConfigureAwait(false);
        }

        [SlashCommand("resume", description: "Resumes the player.", runMode: RunMode.Async)]
        public async Task ResumeAsync()
        {
            var player = await GetPlayerAsync(connectToVoiceChannel: false);

            if (player is null)
            {
                return;
            }

            if (player.State is not PlayerState.Paused)
            {
                await RespondAsync("Player is not paused.").ConfigureAwait(false);
                return;
            }

            await player.ResumeAsync().ConfigureAwait(false);
            await RespondAsync("Resumed.").ConfigureAwait(false);
        }

        [SlashCommand("np", description: "Displays the currently playing track.", runMode: RunMode.Async)]
        public async Task NowPlayingAsync()
        {
            try
            {
                var player = await GetPlayerAsync(connectToVoiceChannel: false);

                if (player is null)
                {
                    return;
                }

                if (player.CurrentItem is null)
                {
                    await RespondAsync("Nothing playing!", ephemeral: true).ConfigureAwait(false);
                    return;
                }

                var track = player.CurrentItem;

                if (track is not null)
                {
                    await RespondAsync(embed: await BuildCurrentlyPlayingEmbed(track, player, _artworkService), ephemeral: true).ConfigureAwait(false);
                }
                else
                {
                    await RespondAsync("Current track is nothing? Report this to the developer.").ConfigureAwait(false);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, ex.Message);
                throw;
            }
        }

        [SlashCommand("status", "Dumps cool info about the current state of the bot.", runMode: RunMode.Async)]
        public async Task StatusAsync()
        {
            var player = await GetPlayerAsync().ConfigureAwait(false);
            bool playerExists = true;
            if (player is null)
            {
                playerExists = false;
            }
            if (Context.User is SocketGuildUser guildUser)
            {
                bool isAdmin = guildUser.GuildPermissions.Administrator;

                if (!isAdmin)
                    await RespondAsync("You need to have admin permissions to execute this command.").ConfigureAwait(false);
            }
            StringBuilder sb = new StringBuilder();
            sb.AppendLine("Player Exists: " + playerExists);
            if (playerExists)
            {
                sb.AppendLine("Player State: " + player!.State.ToString());
                sb.AppendLine("Player API Client: " + player!.ApiClient.Endpoints.BaseAddress);
                sb.AppendLine("Player API Information: " + player!.ApiClient.Endpoints.Information);
                sb.AppendLine("Player Is Paused: " + player!.IsPaused);
                sb.AppendLine("Player Position: " + player!.Position);
                sb.AppendLine("Player Repeat Mode: " + player!.RepeatMode.ToString());
                if (player.VoiceServer.HasValue)
                    sb.AppendLine("Player Voice Server: " + player!.VoiceServer.Value.Endpoint.ToString());
                sb.AppendLine("Player Voice Channel Id: " + player!.VoiceState.VoiceChannelId);
                sb.AppendLine("Player Connection State: " + player!.ConnectionState.ToString());
                if (player.CurrentItem != null)
                {
                    sb.AppendLine("Player Current Item: " + player!.CurrentItem.ToString());
                    if (player.CurrentItem.Track != null)
                    {
                        sb.AppendLine("Player Current Track: " + player!.CurrentItem.Reference.Track!.Uri!.OriginalString);
                    }
                }
                sb.AppendLine("Player Tracks in Queue: " + player!.Queue.Count());
            }


            await RespondAsync(
            embed: new EmbedBuilder()
            {
                Title = "Status",
                Description = sb.ToString()
            }.Build()).ConfigureAwait(false);
        }
    }
}
