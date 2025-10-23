using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace ServConnect.Models
{
    public class ServiceOtp
    {
        [BsonId]
        [BsonRepresentation(BsonType.ObjectId)]
        public string? Id { get; set; }

        [BsonRepresentation(BsonType.ObjectId)]
        public string BookingId { get; set; } = string.Empty;

        [BsonRepresentation(BsonType.String)]
        public Guid UserId { get; set; } // User who will receive the OTP

        [BsonRepresentation(BsonType.String)]
        public Guid ProviderId { get; set; } // Provider who will enter the OTP

        public string OtpCode { get; set; } = string.Empty; // 6-digit code
        
        public DateTime GeneratedAt { get; set; } = DateTime.UtcNow;
        
        public DateTime ExpiresAt { get; set; } // 10 minutes from generation
        
        public bool IsUsed { get; set; } = false;
        
        public DateTime? UsedAt { get; set; }
        
        public string ServiceName { get; set; } = string.Empty;
        
        public string ProviderName { get; set; } = string.Empty;
        
        // Helper property to check if OTP is valid
        public bool IsValid => !IsUsed && DateTime.UtcNow <= ExpiresAt;
    }
}
