using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace ServConnect.Models
{
    public static class ComplaintStatus
    {
        public const string New = "New";
        public const string InProgress = "In Progress";
        public const string Resolved = "Resolved";
    }

    public static class ComplaintRole
    {
        public const string User = RoleTypes.User;
        public const string ServiceProvider = RoleTypes.ServiceProvider;
        public const string Vendor = RoleTypes.Vendor;
        public const string Admin = RoleTypes.Admin; // used for responder meta if needed
    }

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

        // Service Provider Details (optional)
        public Guid? ServiceProviderId { get; set; }
        public string? ServiceProviderName { get; set; }
        public string? ServiceType { get; set; }

        // Category
        public string Category { get; set; } = string.Empty;
        public string? OtherCategoryText { get; set; }

        // Incident Details
        public string Description { get; set; } = string.Empty;
        public List<string> EvidenceFiles { get; set; } = new(); // relative URLs under wwwroot

        // Admin workflow
        public string Status { get; set; } = ComplaintStatus.New;
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
        public string? AdminNote { get; set; }
    }
}