using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace ServConnect.Models.Community
{
    /// <summary>
    /// Represents a like on a comment
    /// </summary>
    public class CommentLike
    {
        [BsonId]
        [BsonRepresentation(BsonType.ObjectId)]
        public string Id { get; set; } = ObjectId.GenerateNewId().ToString();

        [BsonRepresentation(BsonType.ObjectId)]
        public string CommentId { get; set; } = string.Empty;

        [BsonRepresentation(BsonType.String)]
        public Guid UserId { get; set; }
        
        public string UserName { get; set; } = string.Empty;

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
}
