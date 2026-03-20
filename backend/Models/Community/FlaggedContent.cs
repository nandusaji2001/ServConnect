using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace ServConnect.Models.Community
{
    public class FlaggedContent
    {
        [BsonId]
        [BsonRepresentation(BsonType.ObjectId)]
        public string Id { get; set; } = ObjectId.GenerateNewId().ToString();

        public Guid UserId { get; set; }
        public string Username { get; set; } = string.Empty;
        public string UserEmail { get; set; } = string.Empty;

        public string ContentType { get; set; } = string.Empty; // "post" or "comment"
        public string Content { get; set; } = string.Empty; // The text that was flagged
        public List<string> MediaUrls { get; set; } = new(); // Images that were attempted

        public double ToxicityScore { get; set; }
        public double FinalRiskScore { get; set; }
        public string Reason { get; set; } = string.Empty;

        public DateTime FlaggedAt { get; set; } = DateTime.UtcNow;
        public bool ReviewedByAdmin { get; set; } = false;
        public DateTime? ReviewedAt { get; set; }
        public string? ReviewedBy { get; set; }
        public string? AdminNotes { get; set; }
    }
}
