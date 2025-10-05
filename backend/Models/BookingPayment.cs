using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace ServConnect.Models
{
    public enum BookingPaymentStatus
    {
        Pending,
        Paid,
        Failed,
        Refunded
    }

    public class BookingPayment
    {
        [BsonId]
        [BsonRepresentation(BsonType.ObjectId)]
        public string? Id { get; set; }

        // User who needs to pay
        [BsonRepresentation(BsonType.String)]
        public Guid UserId { get; set; }
        public string UserName { get; set; } = string.Empty;
        public string UserEmail { get; set; } = string.Empty;

        // Booking details
        [BsonRepresentation(BsonType.ObjectId)]
        public string BookingId { get; set; } = string.Empty;
        public string ServiceName { get; set; } = string.Empty;
        public string ProviderName { get; set; } = string.Empty;
        
        // Payment details
        public decimal AmountInRupees { get; set; }
        public int AmountInPaise => (int)(AmountInRupees * 100);
        public string Currency { get; set; } = "INR";

        public BookingPaymentStatus Status { get; set; } = BookingPaymentStatus.Pending;
        
        // Razorpay details
        public string? RazorpayOrderId { get; set; }
        public string? RazorpayPaymentId { get; set; }
        public string? RazorpaySignature { get; set; }

        // Service completion details
        public DateTime ServiceCompletedAt { get; set; }
        public int? UserRating { get; set; } // 1..5
        public string? UserFeedback { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    }
}
