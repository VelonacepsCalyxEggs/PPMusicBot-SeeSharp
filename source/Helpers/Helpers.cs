using Discord;
using KenobiRadio.Shared.Models.FileSystem.Children;
using Lavalink4NET.Artwork;
using Lavalink4NET.Players;
using Lavalink4NET.Players.Vote;
using Lavalink4NET.Rest.Entities.Tracks;
using Lavalink4NET.Tracks;
using Microsoft.AspNetCore.StaticFiles;
using PPMusicBot.Classes;
using PPMusicBot.Models;
using PPMusicBot.Services;
using System.Text;
using static PPMusicBot.Services.KenobiAPISearchEngineService;

namespace PPMusicBot.Helpers
{
    public static class Helpers
    {
        public static async Task<Embed> BuildPlayingEmbed(
            int position,
            PlayerState state,
            ArtworkService? artworkService = null,
            TrackLoadResult? lavalinkResult = null,
            KenobiAPIV2SearchResult? result = null)
        {
            bool isExternal = result == null && artworkService != null;
            bool isKenobiApi = lavalinkResult == null;

            if (isExternal)
            {
                if (lavalinkResult == null)
                    throw new ArgumentNullException(nameof(lavalinkResult));
                return await BuildExternalEmbed(position, state == PlayerState.Playing, artworkService!, (TrackLoadResult)lavalinkResult!);
            }
            else if (isKenobiApi)
                return BuildKenobiApiEmbed(position, state == PlayerState.Playing, result!);
            else
                throw new ArgumentException("Both Lavalink result/ArtworkService and KenobiAPI result were null.");
        }

        private static async Task<Embed> BuildExternalEmbed(int position, bool isPlaying, ArtworkService artworkService, TrackLoadResult result)
        {
            bool posIsZero = position == 0;
            int tweakedPos = posIsZero && !isPlaying ? position : position + 1;

            if (result.IsPlaylist)
            {
                if (result.Tracks.Length == 0 || result.Track is null)
                    throw new Exception("Playlist has no tracks or the first track is null.");

                var totalDuration = TimeSpan.FromTicks(result.Tracks.Sum(t => t.Duration.Ticks));
                return new EmbedBuilder()
                {
                    Title = posIsZero ? "Playing:" : "Added to queue:",
                    Description = $"**{result.Playlist.Name}** with {result.Tracks.Length} tracks.",
                    Footer = new EmbedFooterBuilder
                    {
                        Text = $"Duration: {totalDuration:hh\\:mm\\:ss} | " +
                               $"From position: {tweakedPos} to {tweakedPos + result.Tracks.Length}"
                    },
                    ImageUrl = await BuildImageUrlAsync(artworkService, result.Track)
                }.Build();
            }
            else
            {
                var track = result.Track ?? throw new ArgumentNullException(nameof(result.Track));
                string durationText = track.IsLiveStream ? "∞" : track.Duration.ToString(@"mm\:ss");
                return new EmbedBuilder()
                {
                    Title = posIsZero ? "Playing:" : "Added to queue:",
                    Description = $"**{track.Title}** by **{track.Author}** from **{track.Uri}**",
                    Footer = new EmbedFooterBuilder { Text = $"Duration: {durationText} | Position: {tweakedPos}" },
                    ImageUrl = await BuildImageUrlAsync(artworkService, track)
                }.Build();
            }
        }

        private static Embed BuildKenobiApiEmbed(int position, bool isPlaying, KenobiAPIV2SearchResult result)
        {
            bool posIsZero = position == 0;
            int tweakedPos = posIsZero && !isPlaying ? position : position + 1;

            // Album result
            if (result.Albums.Count != 0)
            {
                var album = result.Albums[0];
                var allTracks = album.Discs.SelectMany(d => d.Tracks).ToList();
                if (allTracks.Count == 0)
                    throw new ArgumentOutOfRangeException("Album has no tracks.");

                var totalDuration = TimeSpan.FromTicks(allTracks.Sum(t => t.Duration.Ticks));
                return new EmbedBuilder()
                {
                    Title = posIsZero ? "Playing:" : "Added to queue:",
                    Description = $"**{album.Name}** with {allTracks.Count} tracks.",
                    Footer = new EmbedFooterBuilder
                    {
                        Text = $"Duration: {totalDuration:hh\\:mm\\:ss} | " +
                               $"From position: {tweakedPos} to {tweakedPos + allTracks.Count}"
                    },
                    ImageUrl = GetImageUrl(album.CoverArt) ?? GetDefaultImageUrl()
                }.Build();
            }

            // Single track result
            if (result.Tracks.Count != 0)
            {
                var track = result.Tracks[0];
                string artist = track.Artists.FirstOrDefault()?.Name ?? "Unknown Artist";
                var album = track.Discs.FirstOrDefault()?.Album;
                string durationText = track.Duration.ToString(@"mm\:ss");

                return new EmbedBuilder()
                {
                    Title = posIsZero ? "Playing:" : "Added to queue:",
                    Description = $"**{track.Title}** by **{artist}** from **{album!.NameTransliterated}**",
                    Footer = new EmbedFooterBuilder { Text = $"Duration: {durationText} | Position: {tweakedPos}" },
                    ImageUrl = GetImageUrl(track.CoverArt) ?? GetImageUrl(album!.CoverArt, "album") ?? GetDefaultImageUrl()
                }.Build();
            }

            throw new ArgumentOutOfRangeException(nameof(result), "No tracks or albums found.");
        }

        public static async Task<Embed> BuildCurrentlyPlayingEmbed(ITrackQueueItem item, VoteLavalinkPlayer player, ArtworkService? artworkService)
        {
            var customQueueItem = item.GetCustomData();
            var referenceTrack = item.Track;

            if (referenceTrack is null)
                throw new ArgumentNullException(nameof(referenceTrack));

            if (customQueueItem is CustomQueueTrackItem customItem)
            {
                var kenobiTrack = customItem.CustomTrack;
                string artist = kenobiTrack.Artists.FirstOrDefault()?.Name ?? "Unknown Artist";
                var album = kenobiTrack.Discs.FirstOrDefault()?.Album;
                string durationText = kenobiTrack.Duration.ToString(@"mm\:ss");

                return new EmbedBuilder()
                {
                    Title = "Currently playing:",
                    Description = $"**{kenobiTrack.Title}** by **{artist}** from **{album}**",
                    Footer = new EmbedFooterBuilder
                    {
                        Text = $"Duration: {durationText} | {player.Position?.Position:hh\\:mm\\:ss}"
                    },
                    ImageUrl = GetImageUrl(kenobiTrack.CoverArt) ?? GetImageUrl(album!.CoverArt, "album") ?? GetDefaultImageUrl()
                }.Build();
            }
            else
            {
                bool isLive = referenceTrack.IsLiveStream;
                string durationText = isLive ? "∞" : referenceTrack.Duration.ToString(@"mm\:ss");
                return new EmbedBuilder()
                {
                    Title = "Currently playing:",
                    Description = $"**{referenceTrack.Title}** by **{referenceTrack.Author}** from **{referenceTrack.Uri}**",
                    Footer = new EmbedFooterBuilder
                    {
                        Text = $"Duration: {durationText} | {player.Position?.Position:hh\\:mm\\:ss}"
                    },
                    ImageUrl = await BuildImageUrlAsync(artworkService, referenceTrack)
                }.Build();
            }
        }

        public static (Embed, MessageComponent) BuildQueueEmbed(IVoteLavalinkPlayer player, int page)
        {
            const int tracksPerPage = 10;
            int totalTracks = player.Queue.Count;
            int totalPages = (int)Math.Ceiling(totalTracks / (double)tracksPerPage);
            page = Math.Max(0, Math.Min(page, totalPages - 1));

            int startIndex = page * tracksPerPage;
            int endIndex = Math.Min(startIndex + tracksPerPage, totalTracks);

            var sb = new StringBuilder();
            var pageTotalTime = TimeSpan.Zero;

            for (int i = startIndex; i < endIndex; i++)
            {
                var item = player.Queue[i];
                var customItem = item.GetCustomData();
                if (customItem is CustomQueueTrackItem custom)
                {
                    var kenobiTrack = custom.CustomTrack;
                    string artist = kenobiTrack.Artists.FirstOrDefault()?.Name ?? "Unknown Artist";
                    sb.AppendLine($"{i + 1}. {kenobiTrack.Title} by {artist}");
                    pageTotalTime += kenobiTrack.Duration;
                }
                else if (item.Track != null)
                {
                    sb.AppendLine($"{i + 1}. {item.Track.Title} by {item.Track.Author}");
                    pageTotalTime += item.Track.Duration;
                }
                else
                {
                    sb.AppendLine($"{i + 1}. Unknown track");
                }
            }

            var totalTime = TimeSpan.Zero;
            for (int i = 0; i < totalTracks; i++)
            {
                var item = player.Queue[i];
                var customItem = item.GetCustomData();
                if (customItem is CustomQueueTrackItem custom)
                    totalTime += custom.CustomTrack.Duration;
                else if (item.Track != null)
                    totalTime += item.Track.Duration;
            }

            var embed = new EmbedBuilder()
            {
                Title = $"Queue - Page {page + 1}/{totalPages}",
                Description = sb.ToString(),
                Footer = new EmbedFooterBuilder
                {
                    Text = $"Tracks {startIndex + 1}–{endIndex} of {totalTracks} | " +
                           $"Page time: {pageTotalTime:hh\\:mm\\:ss} | Total time: {totalTime:hh\\:mm\\:ss}"
                },
                Color = Color.Blue
            }.Build();

            var components = new ComponentBuilder()
                .WithButton("Previous", "queue_prev", disabled: page == 0)
                .WithButton("Refresh", "queue_refresh")
                .WithButton("Next", "queue_next", disabled: page >= totalPages - 1)
                .Build();

            return (embed, components);
        }

        private static async Task<string> BuildImageUrlAsync(ArtworkService? artworkService, LavalinkTrack track)
        {
            if (artworkService == null)
                return track.ArtworkUri?.OriginalString ?? string.Empty;

            var artworkUri = await artworkService.ResolveAsync(track);
            return artworkUri?.OriginalString ?? string.Empty;
        }

        private static string? GetImageUrl(DatabaseFileChildDto? coverArt, string type = "track")
        {
            if (coverArt is null) return null;
            string fileName = coverArt.Hash + GetExtensionFromMimeType(coverArt.Type);
            return $"https://www.funckenobi42.space/images/coverart/{type}/{fileName}";
        }

        private static string GetExtensionFromMimeType(string type)
        {
            return type switch
            {
                "image/jpeg" => ".jpe",
                "image/jpg" => ".jpe",
                "image/jpe" => ".jpe",
                "image/png" => ".png",
                "image/webp" => ".webp",
                "image/gif" => ".gif",
                _ => ".jpe",
            };
        }

        private static string GetDefaultImageUrl() => "https://www.funckenobi42.space/images/default_image.jpe";
        public static bool CheckIfValidContentType(string type)
        {
            string[] validTypes = { "audio/mpeg", "audio/mpeg3", "audio/mpeg1", "video/mp4", "audio/wav", "audio/x-wav", "audio/ogg", "audio/vorbis", "audio/opus", "audio/flac" };
            return validTypes.Contains(type);
        }

        public static string GetCachePath()
        {
            string appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            string appName = "PPMusicBotLogs";
            return Path.Combine(appData, appName, "cache");
        }
    }
}