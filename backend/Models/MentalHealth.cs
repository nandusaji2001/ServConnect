using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace ServConnect.Models
{
    /// <summary>
    /// Depression Assessment record storing user responses and prediction results
    /// </summary>
    public class DepressionAssessment
    {
        [BsonId]
        [BsonRepresentation(BsonType.ObjectId)]
        public string Id { get; set; } = ObjectId.GenerateNewId().ToString();

        [BsonElement("userId")]
        public string UserId { get; set; } = string.Empty;

        [BsonElement("userName")]
        public string UserName { get; set; } = string.Empty;

        // User Profile
        [BsonElement("gender")]
        public string Gender { get; set; } = string.Empty;

        [BsonElement("age")]
        public int Age { get; set; }

        [BsonElement("isStudent")]
        public bool IsStudent { get; set; }

        // Common Factors
        [BsonElement("sleepDuration")]
        public string SleepDuration { get; set; } = string.Empty;

        [BsonElement("dietaryHabits")]
        public string DietaryHabits { get; set; } = string.Empty;

        [BsonElement("workStudyHours")]
        public int WorkStudyHours { get; set; }

        [BsonElement("financialStress")]
        public int FinancialStress { get; set; }

        [BsonElement("familyHistoryMentalIllness")]
        public bool FamilyHistoryMentalIllness { get; set; }

        [BsonElement("suicidalThoughts")]
        public bool SuicidalThoughts { get; set; }

        // Student-specific
        [BsonElement("academicPressure")]
        public int? AcademicPressure { get; set; }

        [BsonElement("cgpa")]
        public double? CGPA { get; set; }

        [BsonElement("studySatisfaction")]
        public int? StudySatisfaction { get; set; }

        // Working Professional-specific
        [BsonElement("workPressure")]
        public int? WorkPressure { get; set; }

        [BsonElement("jobSatisfaction")]
        public int? JobSatisfaction { get; set; }

        // Location
        [BsonElement("latitude")]
        public double? Latitude { get; set; }

        [BsonElement("longitude")]
        public double? Longitude { get; set; }

        [BsonElement("city")]
        public string? City { get; set; }

        // Prediction Results
        [BsonElement("isDepressed")]
        public bool IsDepressed { get; set; }

        [BsonElement("depressionProbability")]
        public double DepressionProbability { get; set; }

        [BsonElement("severityLevel")]
        public string SeverityLevel { get; set; } = string.Empty;

        [BsonElement("confidence")]
        public double Confidence { get; set; }

        // Metadata
        [BsonElement("assessmentDate")]
        public DateTime AssessmentDate { get; set; } = DateTime.UtcNow;

        [BsonElement("isActive")]
        public bool IsActive { get; set; } = true;

        [BsonElement("wellnessPlanId")]
        public string? WellnessPlanId { get; set; }
    }

    /// <summary>
    /// 7-day Wellness Plan generated for users showing signs of depression
    /// </summary>
    public class WellnessPlan
    {
        [BsonId]
        [BsonRepresentation(BsonType.ObjectId)]
        public string Id { get; set; } = ObjectId.GenerateNewId().ToString();

        [BsonElement("userId")]
        public string UserId { get; set; } = string.Empty;

        [BsonElement("assessmentId")]
        public string AssessmentId { get; set; } = string.Empty;

        [BsonElement("startDate")]
        public DateTime StartDate { get; set; }

        [BsonElement("endDate")]
        public DateTime EndDate { get; set; }

        [BsonElement("days")]
        public List<WellnessDay> Days { get; set; } = new();

        [BsonElement("totalTasks")]
        public int TotalTasks { get; set; }

        [BsonElement("completedTasks")]
        public int CompletedTasks { get; set; }

        [BsonElement("progressPercentage")]
        public double ProgressPercentage { get; set; }

        [BsonElement("isCompleted")]
        public bool IsCompleted { get; set; }

        [BsonElement("planType")]
        public string PlanType { get; set; } = "recovery"; // "recovery" for depressed, "maintenance" for healthy

        [BsonElement("recommendations")]
        public List<string> Recommendations { get; set; } = new();

        [BsonElement("totalStarsEarned")]
        public int TotalStarsEarned { get; set; } = 0;

        [BsonElement("createdAt")]
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        [BsonElement("updatedAt")]
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    }

    /// <summary>
    /// Single day in the wellness plan
    /// </summary>
    public class WellnessDay
    {
        [BsonElement("day")]
        public int Day { get; set; }

        [BsonElement("date")]
        public string Date { get; set; } = string.Empty;

        [BsonElement("dayName")]
        public string DayName { get; set; } = string.Empty;

        [BsonElement("affirmation")]
        public string Affirmation { get; set; } = string.Empty;

        [BsonElement("tasks")]
        public List<WellnessTask> Tasks { get; set; } = new();

        [BsonElement("isCompleted")]
        public bool IsCompleted { get; set; }
    }

    /// <summary>
    /// Individual wellness task
    /// </summary>
    public class WellnessTask
    {
        [BsonElement("id")]
        public string Id { get; set; } = string.Empty;

        [BsonElement("day")]
        public int Day { get; set; }

        [BsonElement("date")]
        public string Date { get; set; } = string.Empty;

        [BsonElement("dayName")]
        public string DayName { get; set; } = string.Empty;

        [BsonElement("category")]
        public string Category { get; set; } = string.Empty;

        [BsonElement("categoryName")]
        public string CategoryName { get; set; } = string.Empty;

        [BsonElement("icon")]
        public string Icon { get; set; } = string.Empty;

        [BsonElement("color")]
        public string Color { get; set; } = string.Empty;

        [BsonElement("title")]
        public string Title { get; set; } = string.Empty;

        [BsonElement("description")]
        public string Description { get; set; } = string.Empty;

        [BsonElement("duration")]
        public string Duration { get; set; } = string.Empty;

        [BsonElement("placeType")]
        public string? PlaceType { get; set; }

        [BsonElement("requiresLocation")]
        public bool RequiresLocation { get; set; }

        [BsonElement("suggestedPlace")]
        public SuggestedPlace? SuggestedPlace { get; set; }

        [BsonElement("isCompleted")]
        public bool IsCompleted { get; set; }

        [BsonElement("completedAt")]
        public DateTime? CompletedAt { get; set; }

        [BsonElement("userNotes")]
        public string? UserNotes { get; set; }

        [BsonElement("rating")]
        public int? Rating { get; set; }

        [BsonElement("starsAwarded")]
        public int StarsAwarded { get; set; } = 10; // Default 10 stars per task
    }

    /// <summary>
    /// Place suggestion from Google Maps API
    /// </summary>
    public class SuggestedPlace
    {
        [BsonElement("placeId")]
        public string PlaceId { get; set; } = string.Empty;

        [BsonElement("name")]
        public string Name { get; set; } = string.Empty;

        [BsonElement("address")]
        public string Address { get; set; } = string.Empty;

        [BsonElement("latitude")]
        public double Latitude { get; set; }

        [BsonElement("longitude")]
        public double Longitude { get; set; }

        [BsonElement("rating")]
        public double? Rating { get; set; }

        [BsonElement("photoUrl")]
        public string? PhotoUrl { get; set; }

        [BsonElement("placeTypes")]
        public List<string>? PlaceTypes { get; set; }

        [BsonElement("isOpen")]
        public bool? IsOpen { get; set; }

        [BsonElement("distance")]
        public string? Distance { get; set; }
    }

    /// <summary>
    /// Mood tracking entry
    /// </summary>
    public class MoodEntry
    {
        [BsonId]
        [BsonRepresentation(BsonType.ObjectId)]
        public string Id { get; set; } = ObjectId.GenerateNewId().ToString();

        [BsonElement("userId")]
        public string UserId { get; set; } = string.Empty;

        [BsonElement("wellnessPlanId")]
        public string? WellnessPlanId { get; set; }

        [BsonElement("mood")]
        public int Mood { get; set; } // 1-5 scale

        [BsonElement("moodLabel")]
        public string MoodLabel { get; set; } = string.Empty; // Very Bad, Bad, Okay, Good, Great

        [BsonElement("notes")]
        public string? Notes { get; set; }

        [BsonElement("activities")]
        public List<string>? Activities { get; set; }

        [BsonElement("entryDate")]
        public DateTime EntryDate { get; set; } = DateTime.UtcNow;
    }
}
