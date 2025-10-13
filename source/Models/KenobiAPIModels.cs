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
            double score { get; set; }
            string resultType { get; set; }
        }

        public class SearchResultsDto
        {
            public List<ScoredTrack> tracks { get; set; } = new List<ScoredTrack>();
            public List<ScoredAlbum> albums { get; set; } = new List<ScoredAlbum>();
            public List<ScoredArtist> artists { get; set; } = new List<ScoredArtist>();
        }

        public class ApiResponseDto<T>
        {
            public T? data { get; set; }
            public int amount { get; set; }
        }

        public class ScoredTrack : MusicTrack, IScoredItem
        {
            public double score { get; set; }
            public required string resultType { get; set; }
        }

        public class ScoredAlbum : Album, IScoredItem
        {
            public double score { get; set; }
            public required string resultType { get; set; }
        }

        public class ScoredArtist : Artist, IScoredItem
        {
            public double score { get; set; }
            public required string resultType { get; set; }
        }

        // Base models with lowercase properties
        public class MusicTrack
        {
            public string id { get; set; } = string.Empty;
            public string title { get; set; } = string.Empty;
            public string titleLower { get; set; } = string.Empty;
            public string artistId { get; set; } = string.Empty;
            public string albumId { get; set; } = string.Empty;
            public int duration { get; set; }
            public string? uploaderId { get; set; }
            public DateTime uploadedAt { get; set; }
            public int timesPlayed { get; set; }
            public MusicMetadata? MusicMetadata { get; set; }
            public List<MusicFile> MusicFile { get; set; } = new List<MusicFile>();
            public List<MusicFile> files { get; set; } = new List<MusicFile>();
            public Artist artist { get; set; } = new Artist();
            public Album album { get; set; } = new Album();
            public User? uploader { get; set; }
            public List<MusicErrors> MusicErrors { get; set; } = new List<MusicErrors>();
            public List<MusicAnalytics> MusicAnalytics { get; set; } = new List<MusicAnalytics>();
        }

        public class MusicMetadata
        {
            public string musicId { get; set; } = string.Empty;
            public string? coverArtId { get; set; }
            public MusicFile? coverArt { get; set; }
            public string? publisher { get; set; }
            public string? genre { get; set; }
            public int? year { get; set; }
            public int? trackNumber { get; set; }
            public string? discNumber { get; set; }
            public string? composer { get; set; }
            public string? lyricist { get; set; }
            public string? conductor { get; set; }
            public string? remixer { get; set; }
            public int? bpm { get; set; }
            public string? key { get; set; }
            public string? language { get; set; }
            public string? copyright { get; set; }
            public string? license { get; set; }
            public string? isrc { get; set; }
            public string? encodedBy { get; set; }
            public string? encoderSoftware { get; set; }
            public int? bitrate { get; set; }
            public int? sampleRate { get; set; }
            public int? channels { get; set; }
        }

        public class MusicFile
        {
            public string id { get; set; } = string.Empty;
            public string? musicId { get; set; }
            public string filePath { get; set; } = string.Empty;
            public long fileSize { get; set; }
            public string fileHash { get; set; } = string.Empty;
            public string type { get; set; } = string.Empty;
            public string name { get; set; } = string.Empty;
            public bool exists { get; set; }
            public string? ownerId { get; set; }
            public User? owner { get; set; }
            public DateTime createdAt { get; set; }
            public DateTime? updatedAt { get; set; }
        }

        public class Album
        {
            public string id { get; set; } = string.Empty;
            public string name { get; set; } = string.Empty;
            public string nameLower { get; set; } = string.Empty;
            public DateTime createdAt { get; set; }
            public DateTime updatedAt { get; set; }
            public string? coverArtId { get; set; }
            public List<MusicFile> coverArt { get; set; } = new List<MusicFile>();
            public string? primaryOwnerId { get; set; }
            public User? primaryOwnedAlbums { get; set; }
            public List<User> Owners { get; set; } = new List<User>();
            public List<Artist> Artists { get; set; } = new List<Artist>();
            public List<MusicTrack> Music { get; set; } = new List<MusicTrack>();
            public List<AlbumErrors> AlbumErrors { get; set; } = new List<AlbumErrors>();
        }

        public class Artist
        {
            public string id { get; set; } = string.Empty;
            public string name { get; set; } = string.Empty;
            public string nameLower { get; set; } = string.Empty;
            public DateTime createdAt { get; set; }
            public DateTime updatedAt { get; set; }
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
