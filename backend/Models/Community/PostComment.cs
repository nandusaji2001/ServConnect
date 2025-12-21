using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace ServConnect.Models.Community
{
    /// <summary>
    /// Represents a comment on a post with nested reply support
    /// </summary>
    public class PostComment
    {
        [BsonId]
        [BsonRepresentation(BsonType.ObjectId)]
        public string Id { get; set; } = ObjectId.GenerateNewId().ToString();

        [BsonRepresentation(BsonType.ObjectId)]
        public string PostId { get; set; } = string.Empty;

        [BsonRepresentation(BsonType.String)]
        public Guid AuthorId { get; set; }
        
        public string AuthorName { get; set; } = string.Empty;
        public string? AuthorProfileImage { get; set; }
        public string? AuthorUsername { get; set; }

        public string Content { get; set; } = string.Empty;

        // For nested replies - null means top-level comment
        [BsonRepresentation(BsonType.ObjectId)]
        public string? ParentCommentId { get; set; }

        // Engagement
        public int LikesCount { get; set; } = 0;
        public int RepliesCount { get; set; } = 0;

        // Timestamps
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime? UpdatedAt { get; set; }

        // Moderation
        public bool IsDeleted { get; set; } = false;
        public bool IsHidden { get; set; } = false;
        public bool IsFlagged { get; set; } = false;
        public int ReportCount { get; set; } = 0;
    }
}
