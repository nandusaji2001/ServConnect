using System.ComponentModel.DataAnnotations;
using ServConnect.Models;

namespace ServConnect.ViewModels
{
    public class FirebaseRegisterViewModel
    {
        [Required]
        public string IdToken { get; set; } = string.Empty;
        
        [Required]
        [Display(Name = "Full Name")]
        public string Name { get; set; } = string.Empty;
        
        [Required]
        [EmailAddress]
        public string Email { get; set; } = string.Empty;
        
        [Display(Name = "Phone Number")]
        [OptionalPhone(ErrorMessage = "Please enter a valid phone number or leave empty.")]
        public string? PhoneNumber { get; set; }
        
        [Display(Name = "Address")]
        public string? Address { get; set; }
        
        [Required]
        [Display(Name = "Role")]
        public string Role { get; set; } = RoleTypes.User;
        
        public string? ReturnUrl { get; set; }

        // Optional photo URL from Firebase client (fallback if token lacks picture claim)
        public string? PhotoUrl { get; set; }
    }

    // Custom validation attribute for optional phone numbers
    public class OptionalPhoneAttribute : ValidationAttribute
    {
        public override bool IsValid(object? value)
        {
            if (value == null || string.IsNullOrWhiteSpace(value.ToString()))
            {
                return true; // Allow empty/null values
            }

            var phoneNumber = value.ToString()!;
            
            // Remove all non-digit characters except +
            var cleanPhone = System.Text.RegularExpressions.Regex.Replace(phoneNumber, @"[^\d+]", "");
            
            // Check if it's a valid phone number (basic validation)
            // Allow numbers with 7-15 digits, optionally starting with +
            return System.Text.RegularExpressions.Regex.IsMatch(cleanPhone, @"^(\+?\d{7,15})$");
        }
    }
}