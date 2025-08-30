using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace ServConnect.Models
{
    // Represents a custom (non-predefined) service name added by providers
    public class ServiceDefinition
    {
        [BsonId]
        [BsonRepresentation(BsonType.ObjectId)]
        public string Id { get; set; } = null!;

        // Display name (e.g., "AC Repair")
        public string Name { get; set; } = string.Empty;

        // Normalized key for matching (lowercase, hyphenated)
        public string Slug { get; set; } = string.Empty;

        // Optional: which provider added this service first
        [BsonRepresentation(BsonType.String)]
        public Guid? CreatedByProviderId { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
}