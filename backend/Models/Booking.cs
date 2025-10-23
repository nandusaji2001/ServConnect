using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace ServConnect.Models
{
    public enum BookingStatus
    {
        Pending = 0,
        Accepted = 1,
        Rejected = 2
    }

    public enum ServiceStatus
    {
        NotStarted = 0,     // Service accepted but not started
        InProgress = 1,     // Service started by provider
        Completed = 2,      // Service completed by provider
        Cancelled = 3       // Service cancelled
    }

    public class Booking
    {
        [BsonId]
        [BsonRepresentation(BsonType.ObjectId)]
        public string? Id { get; set; }

        // Who requested
        [BsonRepresentation(BsonType.String)]
        public Guid UserId { get; set; }
        public string UserName { get; set; } = string.Empty;
        public string UserEmail { get; set; } = string.Empty;

        // Provider info
        [BsonRepresentation(BsonType.String)]
        public Guid ProviderId { get; set; }
        public string ProviderName { get; set; } = string.Empty;

        // Service link info
        [BsonRepresentation(BsonType.ObjectId)]
        public string ProviderServiceId { get; set; } = string.Empty;
        public string ServiceName { get; set; } = string.Empty;

        // Request details
        public DateTime RequestedAtUtc { get; set; } = DateTime.UtcNow;
        public DateTime ServiceDateTime { get; set; }
        public string ContactPhone { get; set; } = string.Empty;
        public string Address { get; set; } = string.Empty;
        public string? Note { get; set; }

        // Decision
        public BookingStatus Status { get; set; } = BookingStatus.Pending;
        public string? ProviderMessage { get; set; }
        public DateTime? RespondedAtUtc { get; set; }

        // Price information
        public decimal Price { get; set; } = 0;
        public string PriceUnit { get; set; } = "per service";
        public string Currency { get; set; } = "USD";

        // Service execution tracking
        public ServiceStatus ServiceStatus { get; set; } = ServiceStatus.NotStarted;
        public DateTime? ServiceStartedAt { get; set; }
        public DateTime? ServiceCompletedAt { get; set; }
        public string? CurrentOtpId { get; set; } // Links to active ServiceOtp

        // User completion & feedback
        public bool IsCompleted { get; set; } = false; // set by user when service is finished
        public DateTime? CompletedAtUtc { get; set; }
        public int? UserRating { get; set; } // 1..5
        public string? UserFeedback { get; set; }
    }
}