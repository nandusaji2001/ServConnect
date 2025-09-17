using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace ServConnect.Models
{
    public enum AdRequestStatus
    {
        Pending = 0,
        Approved = 1,
        Rejected = 2
    }

    public class AdvertisementRequest
    {
        [BsonId]
        [BsonRepresentation(BsonType.ObjectId)]
        public string Id { get; set; } = string.Empty;

        public Guid RequestedByUserId { get; set; }

        public string ImageUrl { get; set; } = string.Empty; // saved under wwwroot/ads

        public string? TargetUrl { get; set; } // e.g., Google Maps link

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public AdRequestStatus Status { get; set; } = AdRequestStatus.Pending;

        public string? RazorpayPaymentId { get; set; }
        public string? RazorpayOrderId { get; set; }
        public string? RazorpaySignature { get; set; }
        public int AmountInPaise { get; set; } = 100000; // default Rs.1000
        public bool IsPaid { get; set; } = false;

        public string? AdminNote { get; set; }
        public DateTime? ReviewedAtUtc { get; set; }
    }
}