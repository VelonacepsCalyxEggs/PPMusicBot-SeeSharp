using Discord;
using Lavalink4NET.Artwork;
using Lavalink4NET.Players;
using Lavalink4NET.Players.Vote;
using Lavalink4NET.Rest.Entities.Tracks;
using Lavalink4NET.Tracks;
using PPMusicBot.Classes;
using PPMusicBot.Models;
using PPMusicBot.Services;
using System.Text;
using static PPMusicBot.Models.KenobiAPIModels;

namespace PPMusicBot.Helpers
{
    public static class Helpers 
    {
        // This needs a rewrite.
        public static async Task<Embed> BuildPlayingEmbed(
            int position, 
            ArtworkService? artworkService = null, 
            TrackLoadResult? lavalinkResult = null, 
            KenobiAPISearchResult? result = null)
        {
            bool IsExternal = result == null && artworkService == null;
            bool IsKenobiApi = lavalinkResult == null;
            if (IsExternal)
                return await BuildExterbalEmbed(position, (ArtworkService)artworkService!, (TrackLoadResult)lavalinkResult!);
            else if (IsKenobiApi)
                return await BuildKenobiApiEmbed(position, (KenobiAPISearchResult)result!);
            else
                throw new ArgumentException("Both Lavalink result or Artwork Service and KenobiAPI result were null. Can't build embed.");
        }
        private static async Task<Embed> BuildExterbalEmbed(int position, ArtworkService artworkService, TrackLoadResult result)
        {
            bool posIsZero = position == 0;
            int tweakedPos = posIsZero ? position : position + 1;
            if (result.IsPlaylist)
            {
                if (result.Tracks.Length > 0 && result.Track is not null)
                {
                    TimeSpan totalPlaylistDuration = new TimeSpan();
                    foreach (var item in result.Tracks)
                    {
                        totalPlaylistDuration += item.Duration;
                    }
                    return new EmbedBuilder()
                    {
                        Title = posIsZero ? "Playing:" : "Added to queue:",
                        Description = $"**{result.Playlist.Name}** with {result.Tracks.Length} tracks.",
                        Footer = new EmbedFooterBuilder()
                        {
                            Text = $" Duration: {totalPlaylistDuration:hh\\:mm\\:ss} | " +
                            $"From position: {tweakedPos} to {tweakedPos + result.Tracks.Length}"
                        },
                        ImageUrl = await BuildImageUrlAsync(artworkService, result.Track)

                    }.Build();
                }
                else throw new Exception("Result was a playlist, but had no tracks or the first track is null.");
            }
            else
            {
                LavalinkTrack? track = result.Track;
                if (track is not null)
                {
                    string durationText = track.IsLiveStream ? "∞" : track.Duration.ToString(@"mm\:ss");
                    return new EmbedBuilder()
                    {
                        Title = posIsZero ? "Playing:" : "Added to queue:",
                        Description = $"**{track.Title}** by **{track.Author}** from **{track.Uri}**",
                        Footer = new EmbedFooterBuilder()
                        {
                            Text = $" Duration: {durationText} | Position: {tweakedPos}"
                        },
                        ImageUrl = await BuildImageUrlAsync(artworkService, track)
                    }.Build();
                }
                else throw new ArgumentNullException($"Embed track was null. {nameof(track)}");
            }
        }

        private static async Task<Embed> BuildKenobiApiEmbed(int position, KenobiAPISearchResult result)
        {
            bool posIsZero = position == 0;
            int tweakedPos = posIsZero ? position : position + 1;
            if (result.Albums.Count != 0)
            {
                if (result.Albums[0].Music.Count == 0) throw new ArgumentOutOfRangeException("The result indicated album, but there was no music.");
                TimeSpan totalAlbumDuration = new TimeSpan();
                foreach (var item in result.Albums[0].Music)
                {
                    totalAlbumDuration += new TimeSpan(0, 0, item.Duration);
                }
                return new EmbedBuilder()
                {
                    Title = posIsZero ? "Playing:" : "Added to queue:",
                    Description = $"**{result.Albums[0].Name}** with {result.Albums[0].Music.Count} tracks.",
                    Footer = new EmbedFooterBuilder() { Text = $" Duration: {totalAlbumDuration:hh\\:mm\\:ss} | From position: {tweakedPos} to {tweakedPos + result.Albums[0].Music.Count}" },
                    ImageUrl = GetKenobiApiImagePreview(result: result).OriginalString
                }.Build();
            }
            else if (result.Tracks.Count == 1) // Should always be used if 1 track only.
            {
                string durationText = TimeSpan.FromSeconds(result.Tracks[0].Duration).ToString(@"mm\:ss");
                return new EmbedBuilder()
                {
                    Title = IsPlayingOrAdded(posIsZero),
                    Description = $"**{result.Tracks[0].Title}** by **{result.Tracks[0].Artist.Name}** from **{result.Tracks[0].Album.Name}**",
                    Footer = new EmbedFooterBuilder()
                    {
                        Text = $" Duration: {durationText} | Position: {tweakedPos}"
                    },
                    ImageUrl = GetKenobiApiImagePreview(result).OriginalString
                }.Build();
            }
            else throw new ArgumentOutOfRangeException("The result had no albums, or tracks were more or less than 1.");
        }
        // As dumb as this looks, this helps with localization.
        private static string IsPlayingOrAdded(bool positionIsZero)
        {
            if (positionIsZero)
                return "Playing";
            else return "Added to queue.";
        }

        public static async Task<Embed> BuildCurrentlyPlayingEmbed(ITrackQueueItem item, VoteLavalinkPlayer player, ArtworkService? artworkService)
        {
            var track = PlayerExtensions.GetCustomData(item);
            var referenceTrack = item.Track;
            if (referenceTrack is null)
                throw new ArgumentNullException(nameof(referenceTrack));
            TimeSpan finalDuration = TimeSpan.Zero;

            if (track != null)
            {
                finalDuration = TimeSpan.FromSeconds(track.MusicTrack.Duration);
            }
            else if (referenceTrack != null)
            {
                finalDuration = referenceTrack.Duration;
            }

            bool isActuallyLive = (track == null && referenceTrack!.IsLiveStream);
            string durationText = isActuallyLive ? "∞" : finalDuration.ToString(@"mm\:ss");
            if (track is null)
            {
                return new EmbedBuilder()
                {
                    Title = "Currently playing:",
                    Description = $"**{referenceTrack!.Title}** by **{referenceTrack.Author}** from **{referenceTrack.Uri}**",
                    Footer = new EmbedFooterBuilder() { Text = $" Duration: {durationText} | {player.Position?.Position:hh\\:mm\\:ss}" },
                    ImageUrl = await BuildImageUrlAsync(artworkService, referenceTrack)
                }.Build();
            }
            else if (track is not null)
            {
                return new EmbedBuilder()
                {
                    Title = "Currently playing:",
                    Description = $"**{track.MusicTrack.Title}** by **{track.MusicTrack.Artist.Name}** from **{track.MusicTrack.Album.Name}**",
                    Footer = new EmbedFooterBuilder() { Text = $" Duration: {durationText} | {player.Position?.Position:hh\\:mm\\:ss}" },
                    ImageUrl = Helpers.GetKenobiApiImagePreview(inpTrack: track.MusicTrack).OriginalString
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
                    sb.AppendLine($"{i + 1}. {customData?.MusicTrack.Title} by {customData?.MusicTrack.Artist.Name}");
                else
                    sb.AppendLine($"{i + 1}. {track.Track?.Title} by {track.Track?.Author}");
                if (customData != null)
                    pageTotalTime += new TimeSpan(0, 0, customData.MusicTrack.Duration);
                else
                    pageTotalTime += track.Track?.Duration ?? new TimeSpan();

            }

            // This could be expensive... but I likey...
            var totalTime = new TimeSpan();
            for (int i = 0; i < totalTracks; i++)
            {
                var customData = PlayerExtensions.GetCustomData(player.Queue[i]);
                if (customData != null)
                    totalTime += new TimeSpan(0, 0, customData.MusicTrack.Duration);
                else
                    totalTime += (player.Queue[i].Track?.Duration ?? new TimeSpan());
            }

            var embed = new EmbedBuilder()
            {
                Title = $"Queue - Page {page + 1}/{totalPages}",
                Description = sb.ToString(),
                Footer = new EmbedFooterBuilder()
                {
                    Text = $"Tracks {startIndex + 1}-{endIndex} of {totalTracks} | Page time: {pageTotalTime:hh\\:mm\\:ss} | Total time: {totalTime.ToString(@"hh\:mm\:ss")}"
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

        public static Uri GetKenobiApiImagePreview(KenobiAPISearchResult? result = null, MusicTrack? inpTrack = null, KenobiAPIModels.Album? inpAlbum = null)
        {
            string kenobiAlbumimagesPath = "https://www.funckenobi42.space/images/AlbumCoverArt/";
            string kenobiTrackimagesPath = "https://www.funckenobi42.space/images/TrackCoverArt/";
            if (result != null)
            {
                var track = result.Tracks.FirstOrDefault();
                var Album = result.Albums.FirstOrDefault();
                if (track != null)
                {
                    if (track.MusicMetadata?.CoverArt != null)
                    {
                        return new Uri(kenobiTrackimagesPath + track.MusicMetadata.CoverArt.FilePath?.Split('\\').Last());
                    }
                    else if (track.Album.CoverArt.Count != 0)
                    {
                        return new Uri(kenobiAlbumimagesPath + track.Album.CoverArt[0]?.FilePath?.Split('\\').Last());
                    }
                    else
                    {
                        return new Uri(kenobiAlbumimagesPath);
                    }
                }
                else if (Album != null)
                {
                    if (Album.CoverArt.Count != 0)
                    {
                        return new Uri(kenobiAlbumimagesPath + Album.CoverArt[0]?.FilePath?.Split('\\').Last());
                    }
                    else
                    {
                        return new Uri(kenobiAlbumimagesPath);
                    }
                }
                else
                {
                    return new Uri(kenobiAlbumimagesPath);
                }
            }
            else
            {
                if (inpTrack != null)
                {
                    if (inpTrack.MusicMetadata?.CoverArt != null)
                    {
                        return new Uri(kenobiTrackimagesPath + inpTrack.MusicMetadata.CoverArt.FilePath?.Split('\\').Last());
                    }
                    else if (inpTrack.Album.CoverArt.Count != 0)
                    {
                        return new Uri(kenobiAlbumimagesPath + inpTrack.Album.CoverArt[0]?.FilePath?.Split('\\').Last());
                    }
                    else
                    {
                        return new Uri(kenobiAlbumimagesPath);
                    }
                }
                else if (inpAlbum != null)
                {
                    if (inpAlbum.CoverArt.Count != 0)
                    {
                        return new Uri(kenobiAlbumimagesPath + inpAlbum.CoverArt[0]?.FilePath?.Split('\\').Last());
                    }
                    else
                    {
                        return new Uri(kenobiAlbumimagesPath);
                    }
                }
                else
                {
                    return new Uri(kenobiAlbumimagesPath);
                }
            }
        }
        // This should probably be encapsulated inside the play function as a constant. 
        // Remake later.
        public static bool CheckIfValidContentType(string type)
        {
            string[] validTypes = { "audio/mpeg", "video/mp4", "audio/wav", "audio/x-wav", "audio/ogg" };
            if (validTypes.Contains(type)) return true;
            else return false;
        }

        private static async Task<string> BuildImageUrlAsync(ArtworkService? artworkService, LavalinkTrack track)
        {
            if (artworkService == null)
            {
                return track.ArtworkUri?.OriginalString ?? string.Empty;
            }
            var artworkUri = await artworkService.ResolveAsync(track);
            return (artworkUri != null ? artworkUri.OriginalString : String.Empty);
        }

        public static string GetCachePath()
        {
            string appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            string appName = "PPMusicBotLogs";
            return Path.Combine(appData, appName, "cache");
        }
    }
}
