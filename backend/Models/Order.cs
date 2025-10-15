using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace ServConnect.Models
{
    public enum OrderStatus
    {
        Pending = 0,        // payment verified, awaiting vendor acceptance
        Accepted = 1,       // vendor accepted the order
        Packed = 2,         // vendor marked as packed
        Shipped = 3,        // vendor marked as shipped
        OutForDelivery = 4, // vendor marked as out for delivery
        Delivered = 5,      // user confirmed delivery
        Cancelled = 6       // order cancelled
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

        // Detailed shipping address
        public string ShippingAddress { get; set; } = string.Empty; // Legacy field for backward compatibility
        public string ShippingFullName { get; set; } = string.Empty;
        public string ShippingPhoneNumber { get; set; } = string.Empty;
        public string ShippingAddressLine1 { get; set; } = string.Empty;
        public string? ShippingAddressLine2 { get; set; }
        public string ShippingCity { get; set; } = string.Empty;
        public string ShippingState { get; set; } = string.Empty;
        public string ShippingPostalCode { get; set; } = string.Empty;
        public string ShippingCountry { get; set; } = "India";
        public string? ShippingLandmark { get; set; }

        // Reference to user's saved address if used
        [BsonRepresentation(BsonType.ObjectId)]
        public string? UserAddressId { get; set; }

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