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
        public string Role { get; set; } = RoleTypes.User; // User/ServiceProvider/Vendor

        // Provider (optional)
        public Guid? ServiceProviderId { get; set; }
        public string? ServiceProviderName { get; set; }
        public string? ServiceType { get; set; }

        [Required]
        public string Category { get; set; } = string.Empty; // Poor Quality, etc.
        public string? OtherCategoryText { get; set; }

        [Required]
        [StringLength(5000, MinimumLength = 10)]
        public string Description { get; set; } = string.Empty;

        public List<IFormFile> EvidenceFiles { get; set; } = new();
    }

    public class ComplaintListFilter
    {
        public string? Status { get; set; }
        public string? Role { get; set; }
        public string? Category { get; set; }
    }
}