using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace ServConnect.Models
{
    public class LocalService
    {
        [BsonId]
        [BsonRepresentation(BsonType.ObjectId)]
        public string? Id { get; set; }

        public string Name { get; set; } = string.Empty;

        // Category is stored by slug for stable lookup; Name kept for display
        public string CategorySlug { get; set; } = string.Empty;
        public string CategoryName { get; set; } = string.Empty;

        // Google Maps URL instead of lat/lng
        public string? MapUrl { get; set; }

        // Optional contact info
        public string? Address { get; set; }
        public string? Phone { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public bool IsActive { get; set; } = true;
    }
}