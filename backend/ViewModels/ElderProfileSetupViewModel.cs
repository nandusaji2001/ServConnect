using System.ComponentModel.DataAnnotations;

namespace ServConnect.ViewModels
{
    public class ElderProfileSetupViewModel
    {
        public string FullName { get; set; } = string.Empty;

        [Required(ErrorMessage = "Age is required")]
        [Range(50, 120, ErrorMessage = "Age must be between 50 and 120")]
        public int Age { get; set; }

        [Required(ErrorMessage = "Gender is required")]
        public string Gender { get; set; } = string.Empty;

        public string? BloodGroup { get; set; }

        public string? MedicalConditions { get; set; }

        public string? Medications { get; set; }

        [Required(ErrorMessage = "Emergency phone number is required")]
        [Phone(ErrorMessage = "Please enter a valid phone number")]
        [RegularExpression(@"^[6-9]\d{9}$", ErrorMessage = "Please enter a valid 10-digit Indian mobile number")]
        public string EmergencyPhone { get; set; } = string.Empty;

        // Guardian candidate info (populated via AJAX)
        public string? GuardianCandidateId { get; set; }
        public string? GuardianCandidateName { get; set; }
        public bool IsGuardianConfirmed { get; set; } = false;
    }
}
