using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace ServConnect.Models.Community
{
    /// <summary>
    /// Represents a notification for community activities
    /// </summary>
    public class CommunityNotification
    {
        [BsonId]
        [BsonRepresentation(BsonType.ObjectId)]
        public string Id { get; set; } = ObjectId.GenerateNewId().ToString();

        [BsonRepresentation(BsonType.String)]
        public Guid UserId { get; set; } // Recipient

        public CommunityNotificationType Type { get; set; }

        // Actor info
        [BsonRepresentation(BsonType.String)]
        public Guid ActorId { get; set; }
        
        public string ActorName { get; set; } = string.Empty;
        public string? ActorProfileImage { get; set; }

        // Content
        public string Message { get; set; } = string.Empty;

        // Related entity
        [BsonRepresentation(BsonType.ObjectId)]
        public string? RelatedPostId { get; set; }
        
        [BsonRepresentation(BsonType.ObjectId)]
        public string? RelatedCommentId { get; set; }
        
        [BsonRepresentation(BsonType.ObjectId)]
        public string? RelatedMessageId { get; set; }

        // Status
        public bool IsRead { get; set; } = false;
        public DateTime? ReadAt { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }

    public enum CommunityNotificationType
    {
        PostLike,
        PostComment,
        CommentReply,
        CommentLike,
        NewFollower,
        NewMessage,
        Mention,
        PostShared
    }
}
