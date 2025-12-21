using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace ServConnect.Models.Community
{
    /// <summary>
    /// Represents a private message between two users
    /// </summary>
    public class DirectMessage
    {
        [BsonId]
        [BsonRepresentation(BsonType.ObjectId)]
        public string Id { get; set; } = ObjectId.GenerateNewId().ToString();

        // Conversation identifier (sorted combination of user IDs for consistent lookup)
        public string ConversationId { get; set; } = string.Empty;

        [BsonRepresentation(BsonType.String)]
        public Guid SenderId { get; set; }
        
        public string SenderName { get; set; } = string.Empty;
        public string? SenderProfileImage { get; set; }

        [BsonRepresentation(BsonType.String)]
        public Guid ReceiverId { get; set; }
        
        public string ReceiverName { get; set; } = string.Empty;

        // Message content
        public MessageType Type { get; set; } = MessageType.Text;
        public string Content { get; set; } = string.Empty;
        
        // For voice messages
        public string? AudioUrl { get; set; }
        public string? TranscribedText { get; set; }
        public int? AudioDurationSeconds { get; set; }

        // Status
        public bool IsRead { get; set; } = false;
        public DateTime? ReadAt { get; set; }
        
        // Deletion flags (soft delete per user)
        public bool DeletedBySender { get; set; } = false;
        public bool DeletedByReceiver { get; set; } = false;

        // Timestamps
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime? UpdatedAt { get; set; }
    }

    public enum MessageType
    {
        Text,
        Voice,
        Image
    }
}
