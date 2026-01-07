using System.ComponentModel.DataAnnotations;

namespace ServConnect.ViewModels
{
    public class ElderHealthDetailsViewModel
    {
        public string ElderId { get; set; } = string.Empty;
        public string ElderName { get; set; } = string.Empty;
        public int ElderAge { get; set; }
        public string ElderGender { get; set; } = string.Empty;

        [Required(ErrorMessage = "Height is required")]
        [Range(100, 250, ErrorMessage = "Height must be between 100 and 250 cm")]
        [Display(Name = "Height (cm)")]
        public double Height { get; set; }

        [Required(ErrorMessage = "Weight is required")]
        [Range(30, 200, ErrorMessage = "Weight must be between 30 and 200 kg")]
        [Display(Name = "Weight (kg)")]
        public double Weight { get; set; }

        // BMI is calculated automatically
        public double BMI => Height > 0 ? Math.Round(Weight / Math.Pow(Height / 100, 2), 1) : 0;

        [Required(ErrorMessage = "Systolic BP is required")]
        [Range(70, 250, ErrorMessage = "Systolic BP must be between 70 and 250")]
        [Display(Name = "Systolic Blood Pressure")]
        public int SystolicBP { get; set; }

        [Required(ErrorMessage = "Diastolic BP is required")]
        [Range(40, 150, ErrorMessage = "Diastolic BP must be between 40 and 150")]
        [Display(Name = "Diastolic Blood Pressure")]
        public int DiastolicBP { get; set; }

        [Required(ErrorMessage = "Cholesterol level is required")]
        [Range(100, 400, ErrorMessage = "Cholesterol must be between 100 and 400")]
        [Display(Name = "Total Cholesterol (mg/dL)")]
        public double Cholesterol { get; set; }

        [Required(ErrorMessage = "Triglycerides level is required")]
        [Range(50, 500, ErrorMessage = "Triglycerides must be between 50 and 500")]
        [Display(Name = "Triglycerides (mg/dL)")]
        public double Triglycerides { get; set; }

        [Display(Name = "Family History of Type 2 Diabetes")]
        public bool FamilyHistoryT2D { get; set; }

        [Display(Name = "Family History of Heart Disease")]
        public bool FamilyHistoryCVD { get; set; }

        [Required(ErrorMessage = "Sleep hours is required")]
        [Range(3, 12, ErrorMessage = "Sleep hours must be between 3 and 12")]
        [Display(Name = "Average Sleep Hours")]
        public double SleepHours { get; set; }

        [Required(ErrorMessage = "Sleep quality is required")]
        [Display(Name = "Sleep Quality")]
        public string SleepQuality { get; set; } = "Fair";

        [Required(ErrorMessage = "Stress level is required")]
        [Range(1, 10, ErrorMessage = "Stress level must be between 1 and 10")]
        [Display(Name = "Stress Level (1-10)")]
        public double StressLevel { get; set; }

        [Required(ErrorMessage = "Physical activity level is required")]
        [Display(Name = "Physical Activity Level")]
        public string PhysicalActivityLevel { get; set; } = "Sedentary";

        [Required(ErrorMessage = "Diet preference is required")]
        [Display(Name = "Diet Preference")]
        public string DietPreference { get; set; } = "vegetarian";

        [Display(Name = "Food Allergies (comma separated)")]
        public string? FoodAllergies { get; set; }
    }

    public class WellnessPredictionResult
    {
        public bool Success { get; set; }
        public string? Error { get; set; }
        
        // Predictions
        public string DietRecommendation { get; set; } = string.Empty;
        public string DietPlan { get; set; } = string.Empty;
        public string HeartRisk { get; set; } = string.Empty;
        
        // Detailed diet plan
        public DetailedDietPlan? DetailedDiet { get; set; }
        
        // Heart risk details
        public HeartRiskDetails? HeartDetails { get; set; }
        
        // Input summary
        public InputSummary? Summary { get; set; }
    }

    public class DetailedDietPlan
    {
        public string Title { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public List<string> Breakfast { get; set; } = new();
        public List<string> Lunch { get; set; } = new();
        public List<string> Dinner { get; set; } = new();
        public List<string> Snacks { get; set; } = new();
    }

    public class HeartRiskDetails
    {
        public string RiskDescription { get; set; } = string.Empty;
        public List<ExerciseRecommendation> Exercises { get; set; } = new();
        public List<string> DietaryTips { get; set; } = new();
        public List<string> LifestyleTips { get; set; } = new();
        public List<string> Warnings { get; set; } = new();
    }

    public class ExerciseRecommendation
    {
        public string Name { get; set; } = string.Empty;
        public string Duration { get; set; } = string.Empty;
        public string Frequency { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
    }

    public class InputSummary
    {
        public int? Age { get; set; }
        public double? BMI { get; set; }
        public string? BloodPressure { get; set; }
        public double? Cholesterol { get; set; }
        public string? PhysicalActivity { get; set; }
    }
}
