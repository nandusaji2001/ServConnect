using System.ComponentModel.DataAnnotations;
using ServConnect.Models;

namespace ServConnect.ViewModels
{
    public class ComplaintCreateViewModel
    {
        // Complainant Info
        [Required]
        public string Name { get; set; } = string.Empty;
        [Required, EmailAddress]
        public string Email { get; set; } = string.Empty;
        public string? Phone { get; set; }
        [Required]
        public Guid ComplainantId { get; set; }
        [Required]
        public string Role { get; set; } = RoleTypes.User;
        public bool IsElderly { get; set; } = false;

        // Category
        [Required]
        public string Category { get; set; } = string.Empty;
        public string SubCategory { get; set; } = string.Empty;

        // For Service Provider Issue
        public Guid? ServiceProviderId { get; set; }
        public string? ServiceProviderName { get; set; }
        public string? BookingId { get; set; }
        public string? BookingServiceName { get; set; }

        // For Vendor Issue
        public Guid? VendorId { get; set; }
        public string? VendorName { get; set; }
        public string? OrderId { get; set; }
        public string? OrderItemName { get; set; }

        [Required]
        [StringLength(5000, MinimumLength = 10)]
        public string Description { get; set; } = string.Empty;

        public List<IFormFile> EvidenceFiles { get; set; } = new();
    }

    public class ComplaintListFilter
    {
        public string? Status { get; set; }
        public string? Category { get; set; }
        public string? Priority { get; set; }
        public bool? PriorityOnly { get; set; }
    }

    public class ComplaintDetailViewModel
    {
        public Complaint Complaint { get; set; } = null!;
        public List<Users> AdminUsers { get; set; } = new();
    }

    public class ComplaintUpdateViewModel
    {
        [Required]
        public string Id { get; set; } = string.Empty;
        [Required]
        public string Status { get; set; } = string.Empty;
        public string? AdminNote { get; set; }
        public string? RejectionReason { get; set; }
        public string? Resolution { get; set; }
        public string? AssignedTo { get; set; }
    }

    public class UserBookingOption
    {
        public string BookingId { get; set; } = string.Empty;
        public string ServiceName { get; set; } = string.Empty;
        public string ProviderName { get; set; } = string.Empty;
        public Guid ProviderId { get; set; }
        public DateTime CompletedAt { get; set; }
    }

    public class UserOrderOption
    {
        public string OrderId { get; set; } = string.Empty;
        public string ItemName { get; set; } = string.Empty;
        public string VendorName { get; set; } = string.Empty;
        public Guid VendorId { get; set; }
        public DateTime DeliveredAt { get; set; }
    }

    public class ComplaintFormDataViewModel
    {
        public ComplaintCreateViewModel Form { get; set; } = new();
        public List<UserBookingOption> CompletedBookings { get; set; } = new();
        public List<UserOrderOption> DeliveredOrders { get; set; } = new();
    }
}
