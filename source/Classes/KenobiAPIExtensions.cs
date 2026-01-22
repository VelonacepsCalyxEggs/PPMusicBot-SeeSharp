using PPMusicBot.Models;
using System;
using System.Collections.Generic;
using System.Text;

namespace PPMusicBot.Classes
{
    public static class KenobiAPIExtensions
    {
        public static KenobiAPIModels.ScoredTrack ToScoredTrack(this KenobiAPIModels.MusicTrack track)
        {
            return new KenobiAPIModels.ScoredTrack
            {
                Id = track.Id,
                AlbumId = track.AlbumId,
                ArtistId = track.ArtistId,
                UploaderId = track.UploaderId,
                Album = track.Album,
                Artist = track.Artist,
                Duration = track.Duration,
                Score = 0,
                Files = track.Files,
                MusicAnalytics = track.MusicAnalytics,
                MusicFile = track.MusicFile,
                MusicErrors = track.MusicErrors,
                MusicMetadata = track.MusicMetadata,
                ResultType = "Track",
                TimesPlayed = track.TimesPlayed,
                Title = track.Title,
                TitleLower = track.TitleLower,
                UploadedAt = track.UploadedAt,
                Uploader = track.Uploader,
            };
        }
        public static List<KenobiAPIModels.ScoredTrack> ToScoredTrack(this List<KenobiAPIModels.MusicTrack> list)
        {
            return [.. list.Select(t => t.ToScoredTrack())];
        }
    }
}
