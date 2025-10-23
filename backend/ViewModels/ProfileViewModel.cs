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

        [Required(ErrorMessage = "Phone number is required")]
        [RegularExpression("^[6-9][0-9]{9}$", ErrorMessage = "Phone number must be exactly 10 digits starting with 6-9")]
        [Display(Name = "Phone Number")]
        public string? PhoneNumber { get; set; }

        [Required(ErrorMessage = "Address is required")]
        [RegularExpression("^[A-Za-z0-9\\s,\\-.#/()]+$", ErrorMessage = "Address can only contain letters, numbers, spaces, comma, hyphen, dot, hash, slash, and parentheses")]
        [StringLength(200, MinimumLength = 10, ErrorMessage = "Address must be 10–200 characters")]
        [Display(Name = "Complete Address")]
        public string? Address { get; set; }

        [Required(ErrorMessage = "Profile image is required")]
        [Display(Name = "Profile Image")]
        public IFormFile? Image { get; set; }

        [Required(ErrorMessage = "Identity proof is required")]
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