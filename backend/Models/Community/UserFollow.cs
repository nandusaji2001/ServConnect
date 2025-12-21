using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace ServConnect.Models.Community
{
    /// <summary>
    /// Represents a follow relationship between users
    /// </summary>
    public class UserFollow
    {
        [BsonId]
        [BsonRepresentation(BsonType.ObjectId)]
        public string Id { get; set; } = ObjectId.GenerateNewId().ToString();

        // The user who is following
        [BsonRepresentation(BsonType.String)]
        public Guid FollowerId { get; set; }
        
        public string FollowerName { get; set; } = string.Empty;
        public string? FollowerProfileImage { get; set; }

        // The user being followed
        [BsonRepresentation(BsonType.String)]
        public Guid FollowingId { get; set; }
        
        public string FollowingName { get; set; } = string.Empty;
        public string? FollowingProfileImage { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
}
