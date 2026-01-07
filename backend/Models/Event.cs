using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace ServConnect.Models
{
    public class Event
    {
        [BsonId]
        [BsonRepresentation(BsonType.ObjectId)]
        public string? Id { get; set; }

        public string Title { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string Category { get; set; } = string.Empty;
        
        public DateTime StartDateTime { get; set; }
        public DateTime EndDateTime { get; set; }
        
        // Location
        public string Venue { get; set; } = string.Empty;
        public string Address { get; set; } = string.Empty;
        public double Latitude { get; set; }
        public double Longitude { get; set; }
        
        // Capacity & Tickets
        public int Capacity { get; set; }
        public int TicketsSold { get; set; } = 0;
        public bool IsFreeEvent { get; set; } = true;
        public decimal TicketPrice { get; set; } = 0;
        public string Currency { get; set; } = "INR";
        
        // Images
        public List<string> ImageUrls { get; set; } = new();
        public string CoverImageUrl { get; set; } = string.Empty;
        
        // Contact
        public string ContactName { get; set; } = string.Empty;
        public string ContactPhone { get; set; } = string.Empty;
        public string ContactEmail { get; set; } = string.Empty;
        
        // Organizer
        public string OrganizerId { get; set; } = string.Empty;
        public string OrganizerName { get; set; } = string.Empty;
        
        // Status
        public EventStatus Status { get; set; } = EventStatus.Published;
        public bool IsApproved { get; set; } = true;
        public bool IsFeatured { get; set; } = false;
        
        // Metadata
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
        public List<string> Tags { get; set; } = new();
        
        // Computed properties
        [BsonIgnore]
        public int RemainingSeats => Capacity - TicketsSold;
        
        [BsonIgnore]
        public bool IsSoldOut => RemainingSeats <= 0;
        
        [BsonIgnore]
        public bool IsUpcoming => StartDateTime > DateTime.UtcNow;
        
        [BsonIgnore]
        public bool IsOngoing => StartDateTime <= DateTime.UtcNow && EndDateTime >= DateTime.UtcNow;
        
        [BsonIgnore]
        public bool IsEnded => EndDateTime < DateTime.UtcNow;
        
        [BsonIgnore]
        public bool IsActive => Status == EventStatus.Published && !IsEnded;
    }

    public enum EventStatus
    {
        Draft,
        Published,
        Cancelled,
        Completed
    }

    public class EventTicket
    {
        [BsonId]
        [BsonRepresentation(BsonType.ObjectId)]
        public string? Id { get; set; }
        
        public string EventId { get; set; } = string.Empty;
        public string EventTitle { get; set; } = string.Empty;
        public string UserId { get; set; } = string.Empty;
        public string UserName { get; set; } = string.Empty;
        public string UserEmail { get; set; } = string.Empty;
        public string UserPhone { get; set; } = string.Empty;
        
        public int Quantity { get; set; } = 1;
        public decimal TotalAmount { get; set; }
        public bool IsPaid { get; set; }
        public string PaymentId { get; set; } = string.Empty;
        public string PaymentOrderId { get; set; } = string.Empty;
        
        public string TicketCode { get; set; } = string.Empty;
        public TicketStatus Status { get; set; } = TicketStatus.Confirmed;
        
        // QR Code & Verification
        public string QrToken { get; set; } = string.Empty; // Unique secure token for QR verification
        public int VerifiedCount { get; set; } = 0; // How many times verified (for multi-entry tickets)
        public DateTime? LastVerifiedAt { get; set; }
        public string? LastVerifiedBy { get; set; } // Organizer who verified
        public List<TicketVerificationLog> VerificationLogs { get; set; } = new();
        
        // Refund tracking
        public string? RefundStatus { get; set; }
        public string? RefundId { get; set; }
        public DateTime? RefundedAt { get; set; }
        
        public DateTime PurchasedAt { get; set; } = DateTime.UtcNow;
        public DateTime EventDateTime { get; set; }
        
        // Computed property for remaining entries
        [BsonIgnore]
        public int RemainingEntries => Quantity - VerifiedCount;
        
        [BsonIgnore]
        public bool IsFullyUsed => VerifiedCount >= Quantity;
    }
    
    public class TicketVerificationLog
    {
        public DateTime VerifiedAt { get; set; } = DateTime.UtcNow;
        public string VerifiedBy { get; set; } = string.Empty;
        public string VerifiedByName { get; set; } = string.Empty;
        public string Method { get; set; } = string.Empty; // "QR" or "Manual"
    }

    public enum TicketStatus
    {
        Pending,
        Confirmed,
        Cancelled,
        Used
    }
}
