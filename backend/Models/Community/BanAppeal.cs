using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace ServConnect.Models.Community
{
    public enum AppealStatus
    {
        Pending,
        UnderReview,
        Approved,
        Rejected
    }

    public class BanAppeal
    {
        [BsonId]
        [BsonRepresentation(BsonType.ObjectId)]
        public string Id { get; set; } = ObjectId.GenerateNewId().ToString();

        public Guid UserId { get; set; }
        public string Username { get; set; } = string.Empty;
        public string UserEmail { get; set; } = string.Empty;
        public string ContactPhone { get; set; } = string.Empty;

        public string Issue { get; set; } = string.Empty; // User's explanation
        public AppealStatus Status { get; set; } = AppealStatus.Pending;

        public int BanLevel { get; set; }
        public DateTime BanExpiresAt { get; set; }
        public string BanReason { get; set; } = string.Empty;

        public DateTime SubmittedAt { get; set; } = DateTime.UtcNow;
        public DateTime? ReviewedAt { get; set; }
        public string? ReviewedBy { get; set; } // Admin username
        public string? AdminResponse { get; set; }
    }
}
