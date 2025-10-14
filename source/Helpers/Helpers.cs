using Discord;
using Lavalink4NET.Players.Vote;
using Lavalink4NET.Tracks;
using PPMusicBot.Classes;
using PPMusicBot.Models;
using PPMusicBot.Services;
using System;
using System.Collections.Generic;
using System.Diagnostics.Eventing.Reader;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PPMusicBot.Helpers
{
    public static class Helpers
    {
        public static Embed BuildPlayingEmbed(int position, LavalinkTrack? track, KenobiAPISearchResult? result)
        {
            if (track is not null)
            {
                return new EmbedBuilder()
                {
                    Title = position is 0 ? "Playing:" : "Added to queue:",
                    Description = $"**{track.Title}** by **{track.Author}** from **{(result is null ? track.Uri : result.Tracks[0].album.name)}**",
                    Footer = new EmbedFooterBuilder() { Text = $" Duration: {track.Duration.ToString("g")} | Position: {position}" },
                    ImageUrl = result is null ? track.ArtworkUri?.OriginalString : Helpers.GetKenobiApiAlbumPreview(result.Tracks[0].album).OriginalString 

                }.Build();
            }
            else if (result is not null && result.Albums.Count > 0)
            {
                TimeSpan totalAlbumDuration = new TimeSpan();
                foreach (var item in result.Albums[0].Music)
                {
                    totalAlbumDuration += new TimeSpan(0, 0, item.duration);
                }
                return new EmbedBuilder()
                {
                    Title = position is 0 ? "Playing:" : "Added to queue:",
                    Description = $"**{result.Albums[0].name}** with {result.Albums[0].Music.Count} tracks.",
                    Footer = new EmbedFooterBuilder() { Text = $" Duration: {totalAlbumDuration.ToString("g")} | From position: {position} to {position + result.Albums[0].Music.Count - 1}" },
                    ImageUrl = Helpers.GetKenobiApiAlbumPreview(result.Albums[0]).OriginalString

                }.Build();
            }
            else
            {
                throw new ArgumentNullException("No track or album having SearchResult was passed.");
            }
        }

        public static (Embed, MessageComponent) BuildQueueEmbed(IVoteLavalinkPlayer player, int page)
        {
            const int tracksPerPage = 10;
            var totalTracks = player.Queue.Count;
            var totalPages = (int)Math.Ceiling(totalTracks / (double)tracksPerPage);

            page = Math.Max(0, Math.Min(page, totalPages - 1));

            var startIndex = page * tracksPerPage;
            var endIndex = Math.Min(startIndex + tracksPerPage, totalTracks);

            StringBuilder sb = new();
            var pageTotalTime = new TimeSpan();

            for (int i = startIndex; i < endIndex; i++)
            {
                var track = player.Queue[i];
                var customData = PlayerExtensions.GetCustomData(track);

                if (customData != null)
                    sb.AppendLine($"{i + 1}. {customData.title} by {customData.artist.name}");
                else
                    sb.AppendLine($"{i + 1}. {track.Track?.Title} by {track.Track?.Author}");

                pageTotalTime += (track.Track?.Duration ?? new TimeSpan());
            }

            // This could be expensive... but I likey...
            var totalTime = new TimeSpan();
            for (int i = 0; i < totalTracks; i++)
            {
                totalTime += (player.Queue[i].Track?.Duration ?? new TimeSpan());
            }

            var embed = new EmbedBuilder()
            {
                Title = $"Queue - Page {page + 1}/{totalPages}",
                Description = sb.ToString(),
                Footer = new EmbedFooterBuilder()
                {
                    Text = $"Tracks {startIndex + 1}-{endIndex} of {totalTracks} | Page time: {pageTotalTime:g} | Total time: {totalTime:g}"
                },
                Color = Color.Blue
            }.Build();

            var components = new ComponentBuilder()
                .WithButton("Previous", "queue_prev", disabled: page == 0)
                .WithButton("Next", "queue_next", disabled: page >= totalPages - 1)
                .Build();

            return (embed, components);
        }

        public static Uri GetKenobiApiAlbumPreview(KenobiAPIModels.Album album)
        {
            var kenobiAlbumimagesPath = "https://www.funckenobi42.space/images/AlbumCoverArt/";
            if (album.coverArt.Count == 0) return new Uri(kenobiAlbumimagesPath);
            return new Uri(kenobiAlbumimagesPath + album.coverArt[0]?.filePath?.Split('\\').Last());
        }

    }
}
