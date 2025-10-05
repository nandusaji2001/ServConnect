using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace ServConnect.Models
{
    // Links a provider to a specific service (predefined or custom)
    public class ProviderService
    {
        [BsonId]
        [BsonRepresentation(BsonType.ObjectId)]
        public string? Id { get; set; }

        [BsonRepresentation(BsonType.String)]
        public Guid ProviderId { get; set; }

        // Service name to display (either predefined or custom)
        public string ServiceName { get; set; } = string.Empty;

        // Normalized for lookup/matching
        public string ServiceSlug { get; set; } = string.Empty;

        // Service details
        public string Description { get; set; } = string.Empty;
        public decimal Price { get; set; } = 0;
        public string PriceUnit { get; set; } = "per service"; // e.g., "per hour", "per job", "per service"
        public string Currency { get; set; } = "USD";

        // Provider contact information
        public string ProviderName { get; set; } = string.Empty;
        public string ProviderEmail { get; set; } = string.Empty;
        public string ProviderPhone { get; set; } = string.Empty;
        public string ProviderAddress { get; set; } = string.Empty;

        // Service availability
        public bool IsAvailable { get; set; } = true;
        public List<string> AvailableDays { get; set; } = new(); // e.g., ["Monday", "Tuesday", "Wednesday"]
        public string AvailableHours { get; set; } = "9:00 AM - 6:00 PM";

        // Rating and reviews
        public double Rating { get; set; } = 0.0;
        public int ReviewCount { get; set; } = 0;

        public bool IsActive { get; set; } = true;
        
        // Publication and payment details
        public DateTime? PublicationStartDate { get; set; }
        public DateTime? PublicationEndDate { get; set; }
        public bool IsPaid { get; set; } = false;
        public string? PaymentId { get; set; } // Links to ServicePayment
        
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
        
        // Helper property to check if service is expired
        public bool IsExpired => PublicationEndDate.HasValue && PublicationEndDate.Value < DateTime.UtcNow;
    }
}