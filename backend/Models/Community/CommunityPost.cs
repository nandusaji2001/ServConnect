using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace ServConnect.Models.Community
{
    /// <summary>
    /// Represents a post in the community feed with media support
    /// </summary>
    public class CommunityPost
    {
        [BsonId]
        [BsonRepresentation(BsonType.ObjectId)]
        public string Id { get; set; } = ObjectId.GenerateNewId().ToString();

        [BsonRepresentation(BsonType.String)]
        public Guid AuthorId { get; set; }
        
        public string AuthorName { get; set; } = string.Empty;
        public string? AuthorProfileImage { get; set; }
        public string? AuthorUsername { get; set; }

        // Content
        public string Caption { get; set; } = string.Empty;
        public List<string> Hashtags { get; set; } = new();
        
        // Media - supports images and short videos
        public List<PostMedia> Media { get; set; } = new();
        
        // Engagement counters (denormalized for performance)
        public int LikesCount { get; set; } = 0;
        public int CommentsCount { get; set; } = 0;
        public int SharesCount { get; set; } = 0;

        // Timestamps
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime? UpdatedAt { get; set; }
        
        // Moderation
        public bool IsDeleted { get; set; } = false;
        public bool IsHidden { get; set; } = false;
        public bool IsFlagged { get; set; } = false;
        public string? FlagReason { get; set; }
        public int ReportCount { get; set; } = 0;

        // Privacy
        public PostVisibility Visibility { get; set; } = PostVisibility.Public;
    }

    public class PostMedia
    {
        public string Id { get; set; } = ObjectId.GenerateNewId().ToString();
        public MediaType Type { get; set; }
        public string Url { get; set; } = string.Empty;
        public string? ThumbnailUrl { get; set; }
        public int? Width { get; set; }
        public int? Height { get; set; }
        public int? DurationSeconds { get; set; } // For videos
        public long FileSizeBytes { get; set; }
    }

    public enum MediaType
    {
        Image,
        Video
    }

    public enum PostVisibility
    {
        Public,      // Everyone can see
        Followers,   // Only followers can see
        Private      // Only the author can see
    }
}
