using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace ServConnect.Models.Community
{
    /// <summary>
    /// Represents a like/reaction on a post
    /// </summary>
    public class PostLike
    {
        [BsonId]
        [BsonRepresentation(BsonType.ObjectId)]
        public string Id { get; set; } = ObjectId.GenerateNewId().ToString();

        [BsonRepresentation(BsonType.ObjectId)]
        public string PostId { get; set; } = string.Empty;

        [BsonRepresentation(BsonType.String)]
        public Guid UserId { get; set; }
        
        public string UserName { get; set; } = string.Empty;
        public string? UserProfileImage { get; set; }

        public ReactionType ReactionType { get; set; } = ReactionType.Like;

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }

    public enum ReactionType
    {
        Like,
        Love,
        Celebrate,
        Support,
        Insightful
    }
}
