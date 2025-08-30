using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace ServConnect.Models
{
    // Links a provider to a specific service (predefined or custom)
    public class ProviderService
    {
        [BsonId]
        [BsonRepresentation(BsonType.ObjectId)]
        public string Id { get; set; } = null!;

        [BsonRepresentation(BsonType.String)]
        public Guid ProviderId { get; set; }

        // Service name to display (either predefined or custom)
        public string ServiceName { get; set; } = string.Empty;

        // Normalized for lookup/matching
        public string ServiceSlug { get; set; } = string.Empty;

        public bool IsActive { get; set; } = true;
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    }
}