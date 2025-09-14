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

        // User completion & feedback
        public bool IsCompleted { get; set; } = false; // set by user when service is finished
        public DateTime? CompletedAtUtc { get; set; }
        public int? UserRating { get; set; } // 1..5
        public string? UserFeedback { get; set; }
    }
}