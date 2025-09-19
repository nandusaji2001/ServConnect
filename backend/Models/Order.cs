using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace ServConnect.Models
{
    public enum OrderStatus
    {
        Pending = 0,     // payment verified, awaiting shipment
        Shipped = 1,
        Delivered = 2,
        Cancelled = 3
    }

    public enum PaymentStatus
    {
        Created = 0,
        Paid = 1,
        Failed = 2,
        Refunded = 3
    }

    public class Order
    {
        [BsonId]
        [BsonRepresentation(BsonType.ObjectId)]
        public string? Id { get; set; }

        [BsonRepresentation(BsonType.String)]
        public Guid UserId { get; set; }
        public string UserEmail { get; set; } = string.Empty;

        [BsonRepresentation(BsonType.String)]
        public Guid VendorId { get; set; }

        [BsonRepresentation(BsonType.ObjectId)]
        public string ItemId { get; set; } = string.Empty;
        public string ItemTitle { get; set; } = string.Empty;
        public decimal ItemPrice { get; set; }

        public int Quantity { get; set; }
        public decimal TotalAmount { get; set; }

        public string ShippingAddress { get; set; } = string.Empty;

        // flow status
        public OrderStatus Status { get; set; } = OrderStatus.Pending;

        // payment linking
        public string RazorpayOrderId { get; set; } = string.Empty;
        public string? RazorpayPaymentId { get; set; }
        public PaymentStatus PaymentStatus { get; set; } = PaymentStatus.Created;

        // optional shipment tracking URL provided by vendor
        public string? TrackingUrl { get; set; }

        public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
        public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;
    }
}