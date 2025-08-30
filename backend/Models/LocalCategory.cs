using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace ServConnect.Models
{
    public class LocalCategory
    {
        [BsonId]
        [BsonRepresentation(BsonType.ObjectId)]
        public string Id { get; set; } = null!;

        public string Name { get; set; } = string.Empty;
        public string Slug { get; set; } = string.Empty; // normalized

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
}