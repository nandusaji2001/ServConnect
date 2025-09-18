using System.ComponentModel.DataAnnotations;

namespace ServConnect.ViewModels
{
    public class ProfileViewModel
    {
        [Required(ErrorMessage = "Name is required")]
        [RegularExpression("^[A-Za-z\\s]+$", ErrorMessage = "Only alphabets and spaces are allowed.")]
        [StringLength(50, MinimumLength = 3, ErrorMessage = "Full Name must be 3–50 characters.")]
        [Display(Name = "Full Name")]
        public string Name { get; set; } = string.Empty;

        [Required(ErrorMessage = "Email is required")]
        [EmailAddress(ErrorMessage = "Invalid email address")]
        public string Email { get; set; } = string.Empty;

        [Display(Name = "Phone Number")]
        public string? PhoneNumber { get; set; }

        [Display(Name = "Complete Address")]
        [RegularExpression("^[A-Za-z0-9\\s,\\-.]+$", ErrorMessage = "Use letters, numbers, comma, hyphen, dot only.")]
        [StringLength(200, MinimumLength = 10, ErrorMessage = "Address must be 10–200 characters.")]
        public string? Address { get; set; }

        [Display(Name = "Profile Image")]
        public IFormFile? Image { get; set; }

        [Display(Name = "Identity Proof (PDF or Image)")]
        public IFormFile? IdentityProof { get; set; }

        public bool IsProfileCompleted { get; set; }
        public bool IsAdminApproved { get; set; }

        // For password change
        [DataType(DataType.Password)]
        [Display(Name = "Current Password")]
        public string? CurrentPassword { get; set; }

        [DataType(DataType.Password)]
        [Display(Name = "New Password")]
        [StringLength(100, MinimumLength = 8, ErrorMessage = "Password must be at least 8 characters")]
        public string? NewPassword { get; set; }

        [DataType(DataType.Password)]
        [Display(Name = "Confirm New Password")]
        [Compare("NewPassword", ErrorMessage = "Passwords do not match")]
        public string? ConfirmPassword { get; set; }
    }
}