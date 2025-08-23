using System.ComponentModel.DataAnnotations;
using ServConnect.Models; // For RoleTypes

namespace ServConnect.ViewModels
{
    public class RegisterViewModel
    {
        [Required(ErrorMessage = "Name is required.")]
        [Display(Name = "Full Name")]
        public string Name { get; set; } = string.Empty;

        [Required(ErrorMessage = "Email is required.")]
        [EmailAddress]
        public string Email { get; set; } = string.Empty;

        [Required(ErrorMessage = "Phone number is required.")]
        [Phone(ErrorMessage = "Please enter a valid phone number.")]
        [Display(Name = "Phone Number")]
        public string PhoneNumber { get; set; } = string.Empty;

        [Required(ErrorMessage = "Address is required.")]
        [Display(Name = "Address")]
        public string Address { get; set; } = string.Empty;

        [Required(ErrorMessage = "Password is required.")]
        [StringLength(40, MinimumLength = 8, ErrorMessage = "The {0} must be at least {2} and at max {1} characters long.")]
        [DataType(DataType.Password)]
        public string Password { get; set; } = string.Empty;

        [Required(ErrorMessage = "Confirm Password is required.")]
        [DataType(DataType.Password)]
        [Display(Name = "Confirm Password")]
        [Compare("Password", ErrorMessage = "Password does not match.")] // Fixed: Compare to Password
        public string ConfirmPassword { get; set; } = string.Empty;

        /* Role Selection */
        [Required(ErrorMessage = "Role selection is required.")]
        [Display(Name = "Account Type")]
        public string Role { get; set; } = RoleTypes.User; // Default to User
    }

    // Custom validation attribute
    public class RequiredIfAttribute : ValidationAttribute
    {
        private string _propertyName;
        private object _targetValue;

        public RequiredIfAttribute(string propertyName, object targetValue)
        {
            _propertyName = propertyName;
            _targetValue = targetValue;
        }

        protected override ValidationResult IsValid(object value, ValidationContext context)
        {
            var instance = context.ObjectInstance;
            var propertyValue = instance.GetType().GetProperty(_propertyName)?.GetValue(instance);

            if (propertyValue?.Equals(_targetValue) == true && string.IsNullOrWhiteSpace(value?.ToString()))
            {
                return new ValidationResult(ErrorMessage);
            }
            return ValidationResult.Success;
        }
    }
}