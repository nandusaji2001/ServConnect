using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace ServConnect.Models.Community
{
    /// <summary>
    /// Represents a conversation between two users (for listing and quick access)
    /// </summary>
    public class Conversation
    {
        [BsonId]
        [BsonRepresentation(BsonType.ObjectId)]
        public string Id { get; set; } = ObjectId.GenerateNewId().ToString();

        // Sorted combination of user IDs for consistent lookup
        public string ConversationKey { get; set; } = string.Empty;

        // Participants
        [BsonRepresentation(BsonType.String)]
        public Guid User1Id { get; set; }
        
        public string User1Name { get; set; } = string.Empty;
        public string? User1ProfileImage { get; set; }

        [BsonRepresentation(BsonType.String)]
        public Guid User2Id { get; set; }
        
        public string User2Name { get; set; } = string.Empty;
        public string? User2ProfileImage { get; set; }

        // Last message preview
        public string? LastMessageContent { get; set; }
        public MessageType LastMessageType { get; set; } = MessageType.Text;
        
        [BsonRepresentation(BsonType.String)]
        public Guid? LastMessageSenderId { get; set; }
        
        public DateTime LastMessageAt { get; set; } = DateTime.UtcNow;

        // Unread counts per user
        public int UnreadCountUser1 { get; set; } = 0;
        public int UnreadCountUser2 { get; set; } = 0;

        // Block/Mute status
        public bool BlockedByUser1 { get; set; } = false;
        public bool BlockedByUser2 { get; set; } = false;
        public bool MutedByUser1 { get; set; } = false;
        public bool MutedByUser2 { get; set; } = false;

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    }
}
