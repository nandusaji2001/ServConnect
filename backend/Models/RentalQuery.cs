using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace ServConnect.Models
{
    public class RentalQuery
    {
        [BsonId]
        [BsonRepresentation(BsonType.ObjectId)]
        public string Id { get; set; } = ObjectId.GenerateNewId().ToString();

        [BsonRepresentation(BsonType.ObjectId)]
        public string PropertyId { get; set; } = string.Empty;

        public string PropertyTitle { get; set; } = string.Empty;

        // User who asked the query
        public string UserId { get; set; } = string.Empty;
        public string UserName { get; set; } = string.Empty;
        public string UserEmail { get; set; } = string.Empty;

        // Property owner
        public string OwnerId { get; set; } = string.Empty;
        public string OwnerName { get; set; } = string.Empty;

        // Messages in the conversation
        public List<QueryMessage> Messages { get; set; } = new List<QueryMessage>();

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime LastUpdatedAt { get; set; } = DateTime.UtcNow;

        // TTL index field - will auto-delete after 7 days from last update
        [BsonElement("expireAt")]
        public DateTime ExpireAt { get; set; } = DateTime.UtcNow.AddDays(7);

        public bool IsReadByOwner { get; set; } = false;
        public bool IsReadByUser { get; set; } = true; // User created it, so they've read it

        public int UnreadCountForOwner { get; set; } = 1;
        public int UnreadCountForUser { get; set; } = 0;
    }

    public class QueryMessage
    {
        public string Id { get; set; } = ObjectId.GenerateNewId().ToString();
        public string SenderId { get; set; } = string.Empty;
        public string SenderName { get; set; } = string.Empty;
        public string SenderRole { get; set; } = string.Empty; // "User" or "Owner"
        public string Message { get; set; } = string.Empty;
        public DateTime SentAt { get; set; } = DateTime.UtcNow;
    }

    public class CreateQueryRequest
    {
        public string PropertyId { get; set; } = string.Empty;
        public string Message { get; set; } = string.Empty;
    }

    public class ReplyQueryRequest
    {
        public string QueryId { get; set; } = string.Empty;
        public string Message { get; set; } = string.Empty;
    }
}
