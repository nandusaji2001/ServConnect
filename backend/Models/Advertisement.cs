using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace ServConnect.Models
{
    public class Advertisement
    {
        [BsonId]
        [BsonRepresentation(BsonType.ObjectId)]
        public string Id { get; set; } = string.Empty;

        public string ImageUrl { get; set; } = string.Empty; // relative URL under wwwroot

        public string? TargetUrl { get; set; } // optional external link (e.g., Google Maps)

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public bool IsActive { get; set; } = true; // future use for toggling
    }
}