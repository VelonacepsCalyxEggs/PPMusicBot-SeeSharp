using Newtonsoft.Json;
using System.Text.Json.Serialization;
namespace PPMusicBot.Models
{
    // This is what AI is good for, converting Json Objects between languages.
    // But holy shit my Json structure is cursed ngl... and it's gonna be an ass to fix it as well,
    // because that basically would require a database re population, which I woykd hate to do...
    // My bad I guess lol.
    public static class KenobiAPIModels
    {
        public interface IScoredItem
        {
            double Score { get; set; }
            string ResultType { get; set; }
        }

        public class SearchResultsDto
        {
            public List<ScoredTrack> Tracks { get; set; } = new List<ScoredTrack>();
            public List<ScoredAlbum> Albums { get; set; } = new List<ScoredAlbum>();
            public List<ScoredArtist> Artists { get; set; } = new List<ScoredArtist>();
        }

        public class ApiResponseDto<T>
        {
            public T? data { get; set; }
            public int Amount { get; set; }
        }

        public class ScoredTrack : MusicTrack, IScoredItem
        {
            public double Score { get; set; }
            public required string ResultType { get; set; }
        }

        public class ScoredAlbum : Album, IScoredItem
        {
            public double Score { get; set; }
            public required string ResultType { get; set; }
        }

        public class ScoredArtist : Artist, IScoredItem
        {
            public double Score { get; set; }
            public required string ResultType { get; set; }
        }

        // Base models with lowercase properties
        public class MusicTrack
        {
            public string Id { get; set; } = string.Empty;
            public string Title { get; set; } = string.Empty;
            public string TitleLower { get; set; } = string.Empty;
            public string ArtistId { get; set; } = string.Empty;
            public string AlbumId { get; set; } = string.Empty;
            public int Duration { get; set; }
            public string? UploaderId { get; set; }
            public DateTime UploadedAt { get; set; }
            public int TimesPlayed { get; set; }
            public MusicMetadata? MusicMetadata { get; set; }
            public List<MusicFile> MusicFile { get; set; } = new List<MusicFile>();
            public List<MusicFile> Files { get; set; } = new List<MusicFile>();
            public Artist Artist { get; set; } = new Artist();
            public Album Album { get; set; } = new Album();
            public User? Uploader { get; set; }
            public List<MusicErrors> MusicErrors { get; set; } = new List<MusicErrors>();
            public List<MusicAnalytics> MusicAnalytics { get; set; } = new List<MusicAnalytics>();
        }

        public class MusicMetadata
        {
            public string MusicId { get; set; } = string.Empty;
            public string? CoverArtId { get; set; }
            public MusicFile? CoverArt { get; set; }
            public string? Publisher { get; set; }
            public string? Genre { get; set; }
            public int? Year { get; set; }
            public int? TrackNumber { get; set; }
            public string? DiscNumber { get; set; }
            public string? Composer { get; set; }
            public string? Lyricist { get; set; }
            public string? Conductor { get; set; }
            public string? Remixer { get; set; }
            public int? Bpm { get; set; }
            public string? Key { get; set; }
            public string? Language { get; set; }
            public string? Copyright { get; set; }
            public string? License { get; set; }
            public string? Isrc { get; set; }
            public string? EncodedBy { get; set; }
            public string? EncoderSoftware { get; set; }
            public int? Bitrate { get; set; }
            public int? SampleRate { get; set; }
            public int? Channels { get; set; }
        }

        public class MusicFile
        {
            public string Id { get; set; } = string.Empty;
            public string? MusicId { get; set; }
            public string FilePath { get; set; } = string.Empty;
            public string FileWebPath { get; set; } = string.Empty;
            public long FileSize { get; set; }
            public string FileHash { get; set; } = string.Empty;
            public string Type { get; set; } = string.Empty;
            public string Name { get; set; } = string.Empty;
            public bool Exists { get; set; }
            public string? OwnerId { get; set; }
            public User? Owner { get; set; }
            public DateTime CreatedAt { get; set; }
            public DateTime? UpdatedAt { get; set; }
        }

        public class Album
        {
            public string Id { get; set; } = string.Empty;
            public string Name { get; set; } = string.Empty;
            public string NameLower { get; set; } = string.Empty;
            public DateTime CreatedAt { get; set; }
            public DateTime UpdatedAt { get; set; }
            public string? CoverArtId { get; set; }
            public List<MusicFile> CoverArt { get; set; } = new List<MusicFile>();
            public string? PrimaryOwnerId { get; set; }
            public User? PrimaryOwnedAlbums { get; set; }
            public List<User> Owners { get; set; } = new List<User>();
            public List<Artist> Artists { get; set; } = new List<Artist>();
            public List<MusicTrack> Music { get; set; } = new List<MusicTrack>();
            public List<AlbumErrors> AlbumErrors { get; set; } = new List<AlbumErrors>();
        }

        public class Artist
        {
            public string Id { get; set; } = string.Empty;
            public string Name { get; set; } = string.Empty;
            public string NameLower { get; set; } = string.Empty;
            public DateTime CreatedAt { get; set; }
            public DateTime UpdatedAt { get; set; }
            public List<Album>? Albums { get; set; }
            public List<MusicTrack>? Music { get; set; }
        }

        public class MusicAnalytics
        {
            public string UserId { get; set; } = string.Empty;
            public string MusicId { get; set; } = string.Empty;
            public MusicTrack Music { get; set; } = new MusicTrack();
            public User User { get; set; } = new User();
            public int TimesRequested { get; set; }
            public DateTime LastRequestedAt { get; set; }
        }

        public class User
        {
            public string Id { get; set; } = string.Empty;
            public string Email { get; set; } = string.Empty;
            public string Username { get; set; } = string.Empty;
            public string Password { get; set; } = string.Empty;
            public DateTime CreatedAt { get; set; }
            public DateTime UpdatedAt { get; set; }
            public bool Validated { get; set; }
            public string? ValidationToken { get; set; }
            public string? Avatar { get; set; }
            public string? SipNumber { get; set; }
            public List<Role>? Role { get; set; }
            public List<MusicTrack>? Music { get; set; }
            public List<Album>? Album { get; set; }
            public List<MusicFile>? File { get; set; }
            public List<MusicErrors>? MusicErrors { get; set; }
            public List<Album>? PrimaryOwnedAlbums { get; set; }
        }

        public class Role
        {
            public string Id { get; set; } = string.Empty;
            public string Name { get; set; } = string.Empty;
            public List<User> User { get; set; } = new List<User>();
        }

        public class MusicErrors
        {
            public string Id { get; set; } = string.Empty;
            public string MusicId { get; set; } = string.Empty;
            public MusicTrack Music { get; set; } = new MusicTrack();
            public string Error { get; set; } = string.Empty;
            public string CreatedById { get; set; } = string.Empty;
            public User CreatedBy { get; set; } = new User();
            public DateTime CreatedAt { get; set; }
        }

        public class AlbumErrors
        {
            public string Id { get; set; } = string.Empty;
            public string AlbumId { get; set; } = string.Empty;
            public Album Album { get; set; } = new Album();
            public string Error { get; set; } = string.Empty;
            public DateTime CreatedAt { get; set; }
        }

        // API Response classes
        public class MusicApiResponse
        {
            public List<MusicTrack> Data { get; set; } = new List<MusicTrack>();
            public int Amount { get; set; }
        }

        public class AlbumApiResponse
        {
            public List<Album> Data { get; set; } = new List<Album>();
            public int Amount { get; set; }
        }

        public class ArtistApiResponse
        {
            public List<Artist> Data { get; set; } = new List<Artist>();
            public int Amount { get; set; }
        }

        // Query Parameter classes
        public class MusicQueryParams
        {
            public int? Page { get; set; }
            public int? Limit { get; set; }
            public string? SortBy { get; set; }
            public string? SortOrder { get; set; }
            public string? Id { get; set; }
            public string? Title { get; set; }
            public string? ArtistId { get; set; }
            public string? AlbumId { get; set; }
            public string? ArtistName { get; set; }
            public string? AlbumName { get; set; }
            public string? Composer { get; set; }
            public string? Genre { get; set; }
            public int? Year { get; set; }
            public int? FileSize { get; set; }
            public string? FilePath { get; set; }
        }

        public class AlbumQueryParams
        {
            public int? Page { get; set; }
            public int? Limit { get; set; }
            public string? SortBy { get; set; }
            public string? SortOrder { get; set; }
            public string? Id { get; set; }
            public string? Name { get; set; }
            public string? UserId { get; set; }
        }

        public class ArtistQueryParams
        {
            public int? Page { get; set; }
            public int? Limit { get; set; }
            public string? SortOrder { get; set; }
            public string? Id { get; set; }
            public string? Name { get; set; }
        }
    }
}
