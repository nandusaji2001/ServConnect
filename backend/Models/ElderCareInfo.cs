using MongoDbGenericRepository.Attributes;
using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;
using System.ComponentModel.DataAnnotations;

namespace ServConnect.Models
{
    [CollectionName("ElderCareInfo")]
    [BsonIgnoreExtraElements]
    public class ElderCareInfo
    {
        [BsonId]
        [BsonRepresentation(BsonType.ObjectId)]
        public string? Id { get; set; }

        [Required]
        public string UserId { get; set; } = string.Empty;

        [Required]
        public string FullName { get; set; } = string.Empty;

        [Required]
        public string Address { get; set; } = string.Empty;

        [BsonDateTimeOptions(Kind = DateTimeKind.Utc)]
        public DateTime? DateOfBirth { get; set; }

        // Age is now calculated from DateOfBirth
        [BsonIgnore]
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

        [Required]
        public string Gender { get; set; } = string.Empty;

        public string? BloodGroup { get; set; }

        public string? MedicalConditions { get; set; }

        [Required]
        public string Medications { get; set; } = string.Empty;

        [Required]
        public string EmergencyPhone { get; set; } = string.Empty;

        public string? GuardianUserId { get; set; }

        public bool IsGuardianAssigned { get; set; } = false;

        [BsonDateTimeOptions(Kind = DateTimeKind.Utc)]
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        [BsonDateTimeOptions(Kind = DateTimeKind.Utc)]
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    }
}