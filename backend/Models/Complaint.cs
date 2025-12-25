using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace ServConnect.Models
{
    public static class ComplaintStatus
    {
        public const string Pending = "Pending";
        public const string InProgress = "InProgress";
        public const string Resolved = "Resolved";
        public const string Rejected = "Rejected";
    }

    public static class ComplaintCategory
    {
        public const string ServiceProviderIssue = "ServiceProviderIssue";
        public const string VendorIssue = "VendorIssue";
        public const string TechnicalIssue = "TechnicalIssue";
        public const string SafetyEmergency = "SafetyEmergency";
    }

    public static class ComplaintPriority
    {
        public const string Normal = "Normal";
        public const string High = "High";
        public const string Critical = "Critical";
    }

    public static class ComplaintRole
    {
        public const string User = RoleTypes.User;
        public const string ServiceProvider = RoleTypes.ServiceProvider;
        public const string Vendor = RoleTypes.Vendor;
        public const string Admin = RoleTypes.Admin;
    }

    [BsonIgnoreExtraElements]
    public class ComplaintStatusHistory
    {
        public string Status { get; set; } = string.Empty;
        public string? Note { get; set; }
        public string? ChangedBy { get; set; }
        public DateTime ChangedAt { get; set; } = DateTime.UtcNow;
    }

    [BsonIgnoreExtraElements]
    public class Complaint
    {
        [BsonId]
        [BsonRepresentation(BsonType.ObjectId)]
        public string Id { get; set; } = string.Empty;

        // Complainant Info
        public Guid ComplainantId { get; set; }
        public string ComplainantName { get; set; } = string.Empty;
        public string ComplainantEmail { get; set; } = string.Empty;
        public string? ComplainantPhone { get; set; }
        public string ComplainantRole { get; set; } = ComplaintRole.User;
        public bool IsElderly { get; set; } = false;

        // Category & Priority
        public string Category { get; set; } = string.Empty;
        public string SubCategory { get; set; } = string.Empty;
        public string Priority { get; set; } = ComplaintPriority.Normal;

        // Linked Entity (Service Provider or Vendor)
        public Guid? ServiceProviderId { get; set; }
        public string? ServiceProviderName { get; set; }
        public Guid? VendorId { get; set; }
        public string? VendorName { get; set; }

        // Legacy field for backward compatibility
        public string? ServiceType { get; set; }
        public string? OtherCategoryText { get; set; }

        // Linked Booking/Order
        [BsonRepresentation(BsonType.ObjectId)]
        public string? BookingId { get; set; }
        public string? BookingServiceName { get; set; }
        [BsonRepresentation(BsonType.ObjectId)]
        public string? OrderId { get; set; }
        public string? OrderItemName { get; set; }

        // Incident Details
        public string Description { get; set; } = string.Empty;
        public List<string> EvidenceFiles { get; set; } = new();

        // Admin workflow
        public string Status { get; set; } = ComplaintStatus.Pending;
        public string? AssignedTo { get; set; }
        public string? AssignedToName { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
        public string? AdminNote { get; set; }
        public string? RejectionReason { get; set; }
        public string? Resolution { get; set; }
        public DateTime? ResolvedAt { get; set; }

        // Status History
        public List<ComplaintStatusHistory> StatusHistory { get; set; } = new();

        // Suspension tracking
        public bool TargetSuspended { get; set; } = false;
        public DateTime? SuspendedAt { get; set; }
        public string? SuspensionReason { get; set; }
    }
}
