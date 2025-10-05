using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace ServConnect.Models
{
    public enum ServicePaymentStatus
    {
        Pending,
        Paid,
        Failed,
        Refunded
    }

    public class ServicePayment
    {
        [BsonId]
        [BsonRepresentation(BsonType.ObjectId)]
        public string? Id { get; set; }

        [BsonRepresentation(BsonType.String)]
        public Guid ProviderId { get; set; }

        public string ServiceName { get; set; } = string.Empty;
        public string PlanName { get; set; } = string.Empty;
        public int DurationInMonths { get; set; }
        public decimal AmountInRupees { get; set; }
        public int AmountInPaise => (int)(AmountInRupees * 100);

        public ServicePaymentStatus Status { get; set; } = ServicePaymentStatus.Pending;
        
        // Razorpay details
        public string? RazorpayOrderId { get; set; }
        public string? RazorpayPaymentId { get; set; }
        public string? RazorpaySignature { get; set; }

        // Service publication details
        public DateTime? PublicationStartDate { get; set; }
        public DateTime? PublicationEndDate { get; set; }
        public string? ProviderServiceId { get; set; } // Links to ProviderService

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    }
}
