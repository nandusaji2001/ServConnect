using MongoDbGenericRepository.Attributes;
using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;
using System.ComponentModel.DataAnnotations;

namespace ServConnect.Models
{
    [CollectionName("ElderHealthDetails")]
    [BsonIgnoreExtraElements]
    public class ElderHealthDetails
    {
        [BsonId]
        [BsonRepresentation(BsonType.ObjectId)]
        public string? Id { get; set; }

        [Required]
        public string ElderUserId { get; set; } = string.Empty;

        [Required]
        public string GuardianUserId { get; set; } = string.Empty;

        // Physical measurements
        public double Height { get; set; } // in cm
        public double Weight { get; set; } // in kg
        
        [BsonIgnore]
        public double BMI => Height > 0 ? Math.Round(Weight / Math.Pow(Height / 100, 2), 1) : 0;

        // Blood pressure
        public int SystolicBP { get; set; }
        public int DiastolicBP { get; set; }

        // Blood work
        public double Cholesterol { get; set; }
        public double Triglycerides { get; set; }

        // Family history
        public bool FamilyHistoryT2D { get; set; } // Type 2 Diabetes
        public bool FamilyHistoryCVD { get; set; } // Cardiovascular Disease

        // Lifestyle
        public double SleepHours { get; set; }
        public string SleepQuality { get; set; } = "Fair"; // Poor, Fair, Good, Excellent
        public double StressLevel { get; set; } // 1-10 scale
        public string PhysicalActivityLevel { get; set; } = "Sedentary"; // Sedentary, Lightly Active, Moderately Active, Very Active

        // Diet preferences
        public string DietPreference { get; set; } = "vegetarian"; // vegetarian, vegan, keto, high_protein
        public string? FoodAllergies { get; set; }

        // AI Predictions (stored after calculation)
        public string? PredictedDietRecommendation { get; set; }
        public string? PredictedDietPlan { get; set; }
        public string? PredictedHeartRisk { get; set; }
        public DateTime? LastPredictionDate { get; set; }

        [BsonDateTimeOptions(Kind = DateTimeKind.Utc)]
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        [BsonDateTimeOptions(Kind = DateTimeKind.Utc)]
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    }
}
