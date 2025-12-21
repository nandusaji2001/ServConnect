using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace ServConnect.Models.Community
{
    /// <summary>
    /// Represents a banned keyword for content filtering
    /// </summary>
    public class BannedKeyword
    {
        [BsonId]
        [BsonRepresentation(BsonType.ObjectId)]
        public string Id { get; set; } = ObjectId.GenerateNewId().ToString();

        public string Keyword { get; set; } = string.Empty;
        
        // Whether to match whole word only or as substring
        public bool WholeWordOnly { get; set; } = false;

        // Case sensitivity
        public bool CaseSensitive { get; set; } = false;

        // Severity determines action
        public KeywordSeverity Severity { get; set; } = KeywordSeverity.Flag;

        public string? Reason { get; set; }
        
        public bool IsActive { get; set; } = true;

        [BsonRepresentation(BsonType.String)]
        public Guid? AddedByAdminId { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }

    public enum KeywordSeverity
    {
        Flag,      // Flag for review but allow
        Block,     // Block posting immediately
        Shadow     // Allow but hide from others
    }
}
