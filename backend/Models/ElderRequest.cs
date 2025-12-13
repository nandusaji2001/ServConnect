using MongoDbGenericRepository.Attributes;
using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;
using System.ComponentModel.DataAnnotations;

namespace ServConnect.Models
{
    [CollectionName("ElderRequests")]
    public class ElderRequest
    {
        [BsonId]
        [BsonRepresentation(BsonType.ObjectId)]
        public string? Id { get; set; }

        [Required]
        public string ElderUserId { get; set; } = string.Empty;

        [Required]
        public string ElderName { get; set; } = string.Empty;

        [Required]
        public string ElderPhone { get; set; } = string.Empty;

        [Required]
        public string GuardianUserId { get; set; } = string.Empty;

        [Required]
        public string GuardianPhone { get; set; } = string.Empty;

        public string Status { get; set; } = "Pending"; // Pending, Approved, Rejected

        [BsonDateTimeOptions(Kind = DateTimeKind.Utc)]
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        [BsonDateTimeOptions(Kind = DateTimeKind.Utc)]
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    }
}