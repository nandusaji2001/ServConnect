using System.ComponentModel.DataAnnotations;

namespace ServConnect.ViewModels
{
    public class ElderProfileSetupViewModel
    {
        public string FullName { get; set; } = string.Empty;

        [Required(ErrorMessage = "Date of Birth is required")]
        [DataType(DataType.Date)]
        public DateTime? DateOfBirth { get; set; }

        // Calculated age (read-only)
        public int Age
        {
            get
            {
                if (DateOfBirth == null) return 0;
                var today = DateTime.Today;
                var age = today.Year - DateOfBirth.Value.Year;
                if (DateOfBirth.Value.Date > today.AddYears(-age)) age--;
                return age;
            }
        }

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

    // ViewModel for Guardian to edit Elder profile
    public class GuardianEditElderViewModel
    {
        public string ElderId { get; set; } = string.Empty;
        public string FullName { get; set; } = string.Empty;

        [Required(ErrorMessage = "Date of Birth is required")]
        [DataType(DataType.Date)]
        public DateTime? DateOfBirth { get; set; }

        public int Age
        {
            get
            {
                if (DateOfBirth == null) return 0;
                var today = DateTime.Today;
                var age = today.Year - DateOfBirth.Value.Year;
                if (DateOfBirth.Value.Date > today.AddYears(-age)) age--;
                return age;
            }
        }

        [Required(ErrorMessage = "Gender is required")]
        public string Gender { get; set; } = string.Empty;

        public string? BloodGroup { get; set; }

        public string? MedicalConditions { get; set; }

        public string? Medications { get; set; }
    }
}
