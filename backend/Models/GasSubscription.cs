using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace ServConnect.Models
{
    /// <summary>
    /// Gas subscription settings for a user - allows automatic gas booking when levels fall below threshold
    /// </summary>
    public class GasSubscription
    {
        [BsonId]
        [BsonRepresentation(BsonType.ObjectId)]
        public string? Id { get; set; }

        /// <summary>
        /// User who owns this gas subscription
        /// </summary>
        [BsonRepresentation(BsonType.String)]
        public Guid UserId { get; set; }

        /// <summary>
        /// User's full name (denormalized for quick access)
        /// </summary>
        public string UserName { get; set; } = string.Empty;

        /// <summary>
        /// User's email (denormalized for notifications)
        /// </summary>
        public string UserEmail { get; set; } = string.Empty;

        /// <summary>
        /// User's phone number for delivery contact
        /// </summary>
        public string UserPhone { get; set; } = string.Empty;

        /// <summary>
        /// Delivery address for gas cylinder
        /// </summary>
        public string DeliveryAddress { get; set; } = string.Empty;

        /// <summary>
        /// Whether automatic gas booking is enabled
        /// </summary>
        public bool IsAutoBookingEnabled { get; set; } = false;

        /// <summary>
        /// Preferred vendor ID for gas cylinder orders
        /// </summary>
        [BsonRepresentation(BsonType.String)]
        public Guid? PreferredVendorId { get; set; }

        /// <summary>
        /// Preferred vendor's name (denormalized)
        /// </summary>
        public string? PreferredVendorName { get; set; }

        /// <summary>
        /// Gas threshold percentage (default 20% which is ~400g for 2kg cylinder)
        /// When gas level falls below this, auto-booking is triggered
        /// </summary>
        public double ThresholdPercentage { get; set; } = 20.0;

        /// <summary>
        /// Full cylinder weight in grams (for 2kg cylinder = 2000g)
        /// </summary>
        public double FullCylinderWeightGrams { get; set; } = 2000.0;

        /// <summary>
        /// Empty cylinder (tare) weight in grams - weight of cylinder without gas
        /// </summary>
        public double TareCylinderWeightGrams { get; set; } = 500.0;

        /// <summary>
        /// Device ID of the ESP32 monitoring this cylinder
        /// </summary>
        public string? DeviceId { get; set; }

        /// <summary>
        /// Last recorded gas weight in grams
        /// </summary>
        public double LastRecordedWeightGrams { get; set; } = 0;

        /// <summary>
        /// Last gas percentage calculated
        /// </summary>
        public double LastGasPercentage { get; set; } = 0;

        /// <summary>
        /// Timestamp of last weight reading
        /// </summary>
        public DateTime? LastReadingAt { get; set; }

        /// <summary>
        /// Flag to prevent duplicate bookings - set when auto-booking triggers
        /// Reset when delivery is completed
        /// </summary>
        public bool IsBookingPending { get; set; } = false;

        /// <summary>
        /// ID of current pending gas order (if any)
        /// </summary>
        [BsonRepresentation(BsonType.ObjectId)]
        public string? CurrentPendingOrderId { get; set; }

        /// <summary>
        /// Date when last automatic booking was triggered (for once-per-month limit)
        /// </summary>
        public DateTime? LastAutoBookingDate { get; set; }

        /// <summary>
        /// Previous gas status (to detect status drops for email notification)
        /// Values: Full, Good, Half, Low, Critical
        /// </summary>
        public string PreviousGasStatus { get; set; } = "Unknown";

        /// <summary>
        /// Last time a low gas email was sent (to prevent spam within same day)
        /// </summary>
        public DateTime? LastLowGasEmailSentAt { get; set; }

        /// <summary>
        /// When subscription was created
        /// </summary>
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// When subscription was last updated
        /// </summary>
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    }

    /// <summary>
    /// Gas level reading from IoT device
    /// </summary>
    public class GasReading
    {
        [BsonId]
        [BsonRepresentation(BsonType.ObjectId)]
        public string? Id { get; set; }

        /// <summary>
        /// User who owns this subscription
        /// </summary>
        [BsonRepresentation(BsonType.String)]
        public Guid UserId { get; set; }

        /// <summary>
        /// Device ID that sent this reading
        /// </summary>
        public string DeviceId { get; set; } = string.Empty;

        /// <summary>
        /// Raw weight reading in grams
        /// </summary>
        public double WeightGrams { get; set; }

        /// <summary>
        /// Calculated gas percentage
        /// </summary>
        public double GasPercentage { get; set; }

        /// <summary>
        /// Status based on gas level: Full, Half, Low, Critical
        /// </summary>
        public string Status { get; set; } = "Unknown";

        /// <summary>
        /// When reading was recorded
        /// </summary>
        public DateTime Timestamp { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// Optional battery level of IoT device
        /// </summary>
        public int? BatteryLevel { get; set; }
    }

    /// <summary>
    /// Order status for gas cylinder orders
    /// </summary>
    public enum GasOrderStatus
    {
        Pending = 0,
        Accepted = 1,
        OutForDelivery = 2,
        Delivered = 3,
        Cancelled = 4,
        Rejected = 5
    }

    /// <summary>
    /// Gas cylinder order - created when auto-booking triggers or user manually orders
    /// </summary>
    public class GasOrder
    {
        [BsonId]
        [BsonRepresentation(BsonType.ObjectId)]
        public string? Id { get; set; }

        /// <summary>
        /// User who placed the order
        /// </summary>
        [BsonRepresentation(BsonType.String)]
        public Guid UserId { get; set; }

        public string UserName { get; set; } = string.Empty;
        public string UserEmail { get; set; } = string.Empty;
        public string UserPhone { get; set; } = string.Empty;
        public string DeliveryAddress { get; set; } = string.Empty;

        /// <summary>
        /// Vendor who will fulfill the order
        /// </summary>
        [BsonRepresentation(BsonType.String)]
        public Guid VendorId { get; set; }

        public string VendorName { get; set; } = string.Empty;

        /// <summary>
        /// Whether this was triggered automatically by IoT monitoring
        /// </summary>
        public bool IsAutoTriggered { get; set; } = false;

        /// <summary>
        /// Gas level when order was triggered (for auto-triggered orders)
        /// </summary>
        public double? TriggerGasPercentage { get; set; }

        /// <summary>
        /// Gas item ID from vendor's product catalog
        /// </summary>
        [BsonRepresentation(BsonType.ObjectId)]
        public string? GasItemId { get; set; }

        public string GasItemName { get; set; } = "LPG Gas Cylinder";

        /// <summary>
        /// Order price
        /// </summary>
        [BsonRepresentation(BsonType.Decimal128)]
        public decimal Price { get; set; }

        /// <summary>
        /// Order status
        /// </summary>
        public GasOrderStatus Status { get; set; } = GasOrderStatus.Pending;

        /// <summary>
        /// Vendor's message to user (optional)
        /// </summary>
        public string? VendorMessage { get; set; }

        /// <summary>
        /// When order was created
        /// </summary>
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// When order status was last updated
        /// </summary>
        public DateTime? UpdatedAt { get; set; }

        /// <summary>
        /// When vendor accepted the order
        /// </summary>
        public DateTime? AcceptedAt { get; set; }

        /// <summary>
        /// When order went out for delivery
        /// </summary>
        public DateTime? OutForDeliveryAt { get; set; }

        /// <summary>
        /// When order was delivered
        /// </summary>
        public DateTime? DeliveredAt { get; set; }

        /// <summary>
        /// Weight before delivery (for verification)
        /// </summary>
        public double? PreDeliveryWeightGrams { get; set; }

        /// <summary>
        /// Weight after delivery (for verification)
        /// </summary>
        public double? PostDeliveryWeightGrams { get; set; }

        /// <summary>
        /// Whether delivery was verified by weight increase detection
        /// </summary>
        public bool IsDeliveryVerified { get; set; } = false;
    }

    /// <summary>
    /// Request model for registering/updating gas subscription
    /// </summary>
    public class GasSubscriptionRequest
    {
        public bool IsAutoBookingEnabled { get; set; }
        public string? PreferredVendorId { get; set; }
        public double ThresholdPercentage { get; set; } = 20.0;
        public string? DeviceId { get; set; }
        public string DeliveryAddress { get; set; } = string.Empty;
        public string UserPhone { get; set; } = string.Empty;
        public double FullCylinderWeightGrams { get; set; } = 2000.0;
        public double TareCylinderWeightGrams { get; set; } = 500.0;
    }

    /// <summary>
    /// Request model for ESP32 to send weight readings
    /// </summary>
    public class GasReadingRequest
    {
        public double Weight { get; set; } // Weight in kg
        public string? DeviceId { get; set; }
        public int? BatteryLevel { get; set; }
    }

    /// <summary>
    /// Response model for gas subscription dashboard
    /// </summary>
    public class GasSubscriptionDashboard
    {
        public GasSubscription? Subscription { get; set; }
        public double CurrentGasPercentage { get; set; }
        public string GasStatus { get; set; } = "Unknown";
        public double CurrentWeightGrams { get; set; }
        public List<GasReading> RecentReadings { get; set; } = new();
        public GasOrder? CurrentOrder { get; set; }
        public List<GasOrder> OrderHistory { get; set; } = new();
    }
}
