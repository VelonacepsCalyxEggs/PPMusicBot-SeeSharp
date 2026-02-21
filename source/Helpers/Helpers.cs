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
        public static async Task<Embed> BuildPlayingEmbed(int position, TrackLoadResult? lavalinkResult, KenobiAPISearchResult? result, ArtworkService? artworkService)
        {
            if (lavalinkResult is not null && !lavalinkResult.Value.IsPlaylist)
            {
                var track = lavalinkResult.Value.Track;
                if (track is not null)
                {
                    TimeSpan finalDuration = TimeSpan.Zero;

                    if (result != null && result.Tracks.Count > 0)
                    {
                        finalDuration = TimeSpan.FromSeconds(result.Tracks[0].Duration);
                    }
                    else if (lavalinkResult?.Track != null)
                    {
                        finalDuration = lavalinkResult.Value.Track.Duration;
                    }

                    bool resultExists = result != null;

                    bool isActuallyLive = (result == null && track.IsLiveStream); // Having to jump through hoops because backend serves music in chunked mode.
                    string durationText = isActuallyLive ? "∞" : finalDuration.ToString(@"mm\:ss");

                    return new EmbedBuilder()
                    {
                        Title = position is 0 ? "Playing:" : "Added to queue:",
                        Description = $"**{(resultExists ? result!.Tracks[0].Title : track.Title)}** by **{(resultExists ? result!.Tracks[0].Artist.Name : track.Author)}** from **{(resultExists ? result!.Tracks[0].Album.Name : track.Uri)}**",
                        Footer = new EmbedFooterBuilder()
                        {
                            Text = $" Duration: {durationText} | Position: {position + 1}"
                        },
                        ImageUrl = result is null
                            ? (await BuildImageUrlAsync(artworkService, track))
                            : Helpers.GetKenobiApiImagePreview(result).OriginalString
                    }.Build();
                }
                throw new ArgumentNullException(nameof(lavalinkResult.Value.Track));
            }
            else if (lavalinkResult is not null && lavalinkResult.Value.IsPlaylist)
            {
                if (lavalinkResult.Value.Tracks.Length > 0 && lavalinkResult.Value.Track is not null)
                {
                    TimeSpan totalPlaylistDuration = new TimeSpan();
                    foreach (var item in lavalinkResult.Value.Tracks)
                    {
                        totalPlaylistDuration += item.Duration;
                    }
                    return new EmbedBuilder()
                    {
                        Title = position is 0 ? "Playing:" : "Added to queue:",
                        Description = $"**{lavalinkResult.Value.Playlist.Name}** with {lavalinkResult.Value.Tracks.Length} tracks.",
                        Footer = new EmbedFooterBuilder() { Text = $" Duration: {totalPlaylistDuration:hh\\:mm\\:ss} | From position: {position + 1} to {position + 1 + lavalinkResult.Value.Tracks.Length - 1}" },
                        ImageUrl = await BuildImageUrlAsync(artworkService, lavalinkResult.Value.Track)

                    }.Build();
                }
                else
                {
                    throw new ArgumentNullException(nameof(lavalinkResult.Value.Track));
                }
            }
            else if (result is not null && result.Albums.Count > 0)
            {
                TimeSpan totalAlbumDuration = new TimeSpan();
                foreach (var item in result.Albums[0].Music)
                {
                    totalAlbumDuration += new TimeSpan(0, 0, item.Duration);
                }
                return new EmbedBuilder()
                {
                    Title = position is 0 ? "Playing:" : "Added to queue:",
                    Description = $"**{result.Albums[0].Name}** with {result.Albums[0].Music.Count} tracks.",
                    Footer = new EmbedFooterBuilder() { Text = $" Duration: {totalAlbumDuration:hh\\:mm\\:ss} | From position: {position + 1} to {position + 1 + result.Albums[0].Music.Count - 1}" },
                    ImageUrl = Helpers.GetKenobiApiImagePreview(result: result).OriginalString

                }.Build();
            }
            else
            {
                throw new ArgumentNullException("No track or Album having SearchResult was passed.");
            }
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
            string appName = "PPMusicBot";
            return Path.Combine(appData, appName, "cache");
        }
    }
}
