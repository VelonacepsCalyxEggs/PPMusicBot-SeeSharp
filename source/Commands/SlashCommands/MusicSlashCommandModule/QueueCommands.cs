using Discord;
using Discord.Interactions;
using PPMusicBot.Models;
using static PPMusicBot.Helpers.Helpers;
namespace PPMusicBot.Commands.SlashCommands.MusicSlashCommandModule
{
    public sealed partial class MusicSlashCommandModule
    {
        [SlashCommand("shuffle", description: "Shuffles the queue randomly.", runMode: RunMode.Async)]
        public async Task ShuffleAsync()
        {
            var player = await GetPlayerAsync(connectToVoiceChannel: false);

            if (player is null)
            {
                return;
            }

            if (player.Queue.Count is 0)
            {
                await RespondAsync("The queue is empty!", ephemeral: true).ConfigureAwait(false);
                return;
            }

            await player.Queue.ShuffleAsync();
            await RespondAsync("The queue was shuffled!", ephemeral: false).ConfigureAwait(false);
        }

        [SlashCommand("move", description: "Moves a track to a new position.", runMode: RunMode.Async)]
        public async Task MoveAsync(int trackToMove, int position)
        {
            var player = await GetPlayerAsync(connectToVoiceChannel: false);

            if (player is null)
            {
                return;
            }

            if (player.Queue.Count is 0)
            {
                await RespondAsync("The queue is empty!", ephemeral: true).ConfigureAwait(false);
                return;
            }

            if (trackToMove - 1 > player.Queue.Count || trackToMove - 1 < 0)
            {
                await RespondAsync("There is no track on that position.", ephemeral: true).ConfigureAwait(false);
                return;
            }
            var item = player.Queue[trackToMove - 1];
            if (position > player.Queue.Count)
            {
                // move the track to the end of the queue
                await player.Queue.RemoveAtAsync(trackToMove - 1);
                await player.Queue.AddAsync(item);
                await RespondAsync("Moved the track to the end of the queue.").ConfigureAwait(false);
                return;
            }
            else if (position - 1 <= 0)
            {
                // move the track to the first position
                await player.Queue.RemoveAtAsync(trackToMove - 1);
                await player.Queue.InsertAsync(0, item);
                await RespondAsync("Moved the track to the start of the queue.").ConfigureAwait(false);
                return;
            }
            else
            {
                await player.Queue.RemoveAtAsync(trackToMove - 1);
                await player.Queue.InsertAsync(position - 1, item);
                await RespondAsync($"Inserted the track to position number {position}.").ConfigureAwait(false);
                return;
            }
        }

        [SlashCommand("remove", description: "Remove a track or a range of tracks from the queue.", runMode: RunMode.Async)]
        public async Task RemoveAsync(int position1, int? position2 = null)
        {
            var player = await GetPlayerAsync(connectToVoiceChannel: false);

            if (player is null)
            {
                return;
            }

            if (player.Queue.Count is 0)
            {
                await RespondAsync("The queue is empty!", ephemeral: true).ConfigureAwait(false);
                return;
            }

            if (position1 - 1 > player.Queue.Count || position1 - 1 < 0)
            {
                await RespondAsync($"There is nothing on position {position1}!", ephemeral: true).ConfigureAwait(false);
                return;
            }
            if (position1 > player.Queue.Count || position1 - 1 < 0)
            {
                await RespondAsync("The first position is larger than the queue or less than one.").ConfigureAwait(false);
                return;
            }

            if (position2 is null)
            {
                await player.Queue.RemoveAtAsync(position1 - 1);
                await RespondAsync($"Removed the track at position {position1}").ConfigureAwait(false);
                return;
            }
            else if (position2 is not null)
            {
                int startPos = Math.Clamp(position1, 1, player.Queue.Count);
                int endPos = Math.Clamp((int)position2, 1, player.Queue.Count);

                if (startPos > endPos)
                {
                    (startPos, endPos) = (endPos, startPos);
                }

                int startIndex = startPos - 1;
                int amountToRemove = endPos - startPos + 1;

                amountToRemove = Math.Clamp(amountToRemove, 0, player.Queue.Count - startIndex);

                await player.Queue.RemoveRangeAsync(startIndex, amountToRemove);
                await RespondAsync($"Removed tracks from position {startPos} to {endPos}. (removed {amountToRemove} tracks)").ConfigureAwait(false);
            }
        }

        [SlashCommand("queue", description: "Shows the tracks in the queue.", runMode: RunMode.Async)]
        public async Task QueueAsync(int page = 0)
        {
            try
            {
                var player = await GetPlayerAsync(connectToVoiceChannel: false);

                if (player is null)
                {
                    return;
                }

                if (player.Queue.IsEmpty)
                {
                    await RespondAsync("The queue is empty.", ephemeral: true).ConfigureAwait(false);
                    return;
                }

                // queue state for pagination
                var queueState = new QueueState
                {
                    GuildId = Context.Guild.Id,
                    CurrentPage = page,
                    LastUpdated = DateTime.UtcNow
                };

                var (embed, components) = BuildQueueEmbed(player, queueState.CurrentPage);

                if (embed.Description.Length > 4096) // I think the limit was 4096??  Well it didn't crash yet.
                {
                    await RespondAsync("The queue is too long to display.").ConfigureAwait(false);
                    return;
                }

                await RespondAsync(embed: embed, components: components).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, ex.Message);
                throw;
            }
        }

        // I am not sure if these should be here tbh
        // Does this warrant creation of a separate file for the queue command?
        [ComponentInteraction("queue_prev")]
        public async Task HandleQueuePrevious()
        {
            await HandleQueuePagination(-1);
        }


        [ComponentInteraction("queue_next")]
        public async Task HandleQueueNext()
        {
            await HandleQueuePagination(1);
        }

        [ComponentInteraction("queue_refresh")]
        public async Task HandleQueueRefrsh()
        {
            await HandleQueuePagination(0);
        }

        private async Task HandleQueuePagination(int direction)
        {
            await DeferAsync().ConfigureAwait(false);

            var player = await GetPlayerAsync(connectToVoiceChannel: false);
            if (player is null || player.Queue.IsEmpty)
            {
                await ModifyOriginalResponseAsync(msg =>
                {
                    msg.Content = "Queue is no longer available.";
                    msg.Components = new ComponentBuilder().Build();
                }).ConfigureAwait(false);
                return;
            }

            var originalMessage = await GetOriginalResponseAsync();
            if (originalMessage.Embeds.Count == 0)
                throw new ArgumentNullException(nameof(originalMessage.Embeds));
            var currentPage = ExtractCurrentPage(originalMessage.Embeds.FirstOrDefault()!);

            var newPage = currentPage + direction;
            var (embed, components) = BuildQueueEmbed(player, newPage);

            await ModifyOriginalResponseAsync(msg =>
            {
                msg.Embed = embed;
                msg.Components = components;
            }).ConfigureAwait(false);
        }

        private int ExtractCurrentPage(IEmbed embed)
        {
            if (embed?.Title == null) return 0;

            var titleParts = embed.Title.Split(" - Page ");
            if (titleParts.Length == 2)
            {
                var pageParts = titleParts[1].Split('/');
                if (pageParts.Length == 2 && int.TryParse(pageParts[0], out int currentPage))
                {
                    return currentPage - 1; // Convert to 0-based index
                }
            }
            return 0;
        }
    }
}
