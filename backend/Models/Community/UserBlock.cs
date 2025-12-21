using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace ServConnect.Models.Community
{
    /// <summary>
    /// Represents a block relationship between users
    /// </summary>
    public class UserBlock
    {
        [BsonId]
        [BsonRepresentation(BsonType.ObjectId)]
        public string Id { get; set; } = ObjectId.GenerateNewId().ToString();

        // The user who is blocking
        [BsonRepresentation(BsonType.String)]
        public Guid BlockerId { get; set; }

        // The user being blocked
        [BsonRepresentation(BsonType.String)]
        public Guid BlockedUserId { get; set; }
        
        public string BlockedUserName { get; set; } = string.Empty;

        public string? Reason { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
}
