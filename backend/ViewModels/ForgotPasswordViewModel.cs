using System.ComponentModel.DataAnnotations;

namespace ServConnect.ViewModels
{
    public class ForgotPasswordViewModel
    {
        [Required(ErrorMessage = "Phone number is required.")]
        [Phone(ErrorMessage = "Please enter a valid phone number.")]
        [Display(Name = "Phone Number")]
        public string PhoneNumber { get; set; } = string.Empty;
    }
}