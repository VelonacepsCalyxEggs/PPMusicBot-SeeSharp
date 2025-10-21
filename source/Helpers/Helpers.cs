using Discord;
using Lavalink4NET.Artwork;
using Lavalink4NET.Players;
using Lavalink4NET.Players.Vote;
using Lavalink4NET.Tracks;
using PPMusicBot.Classes;
using PPMusicBot.Models;
using PPMusicBot.Services;
using System.Text;

namespace PPMusicBot.Helpers
{
    public static class Helpers
    {
        public static async Task<Embed> BuildPlayingEmbed(int position, LavalinkTrack? track, KenobiAPISearchResult? result, ArtworkService? artworkService)
        {
            if (track is not null)
            {
                return new EmbedBuilder()
                {
                    Title = position is 0 ? "Playing:" : "Added to queue:",
                    Description = $"**{track.Title}** by **{track.Author}** from **{(result is null ? track.Uri : result.Tracks[0].album.name)}**",
                    Footer = new EmbedFooterBuilder() { Text = $" Duration: {track.Duration.ToString(@"hh\:mm\:ss")} | Position: {position}" },
                    ImageUrl = result is null ? ( artworkService is null ? track.ArtworkUri?.OriginalString : (await artworkService.ResolveAsync(track)).OriginalString) : Helpers.GetKenobiApiAlbumPreview(result.Tracks[0].album).OriginalString 

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
                    Footer = new EmbedFooterBuilder() { Text = $" Duration: {totalAlbumDuration.ToString(@"hh\:mm\:ss")} | From position: {position} to {position + result.Albums[0].Music.Count - 1}" },
                    ImageUrl = Helpers.GetKenobiApiAlbumPreview(result.Albums[0]).OriginalString

                }.Build();
            }
            else
            {
                throw new ArgumentNullException("No track or album having SearchResult was passed.");
            }
        }

        public static async Task<Embed> BuildCurrentlyPlayingEmbed(ITrackQueueItem item, VoteLavalinkPlayer player, ArtworkService? artworkService)
        {
            var track = PlayerExtensions.GetCustomData(item);
            if (track is null)
            {
                var referenceTrack = item.Track;
                if (referenceTrack is null)
                    throw new ArgumentNullException(nameof(referenceTrack));
                return new EmbedBuilder()
                {
                    Title = "Currently playing:",
                    Description = $"**{referenceTrack.Title}** by **{referenceTrack.Author}** from **{referenceTrack.Uri}**",
                    Footer = new EmbedFooterBuilder() { Text = $" Duration: {referenceTrack.Duration.ToString(@"hh\:mm\:ss")} | {player.Position?.Position.ToString(@"hh\:mm\:ss")}" },
                    ImageUrl = artworkService is null ? referenceTrack.ArtworkUri?.OriginalString : (await artworkService.ResolveAsync(referenceTrack)).OriginalString,
                }.Build();
            }
            else if (track is not null && track.Reference.Track is not null)
            {
                return new EmbedBuilder()
                {
                    Title = "Currently playing:",
                    Description = $"**{track.MusicTrack.title}** by **{track.MusicTrack.artist.name}** from **{track.MusicTrack.album.name}**",
                    Footer = new EmbedFooterBuilder() { Text = $" Duration: {track.Reference.Track.Duration.ToString(@"hh\:mm\:ss")} | {player.Position?.Position.ToString(@"hh\:mm\:ss")}" },
                    ImageUrl = Helpers.GetKenobiApiAlbumPreview(track.MusicTrack.album).OriginalString
                }.Build();
            }
            else
            {
                throw new ArgumentNullException(nameof(track.Reference.Track));
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
                    sb.AppendLine($"{i + 1}. {customData?.MusicTrack.title} by {customData?.MusicTrack.artist.name}");
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
                    Text = $"Tracks {startIndex + 1}-{endIndex} of {totalTracks} | Page time: {pageTotalTime.ToString(@"hh\:mm\:ss")} | Total time: {totalTime.ToString(@"hh\:mm\:ss")}"
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
