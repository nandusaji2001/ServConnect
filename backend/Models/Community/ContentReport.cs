using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace ServConnect.Models.Community
{
    /// <summary>
    /// Represents a user report for inappropriate content
    /// </summary>
    public class ContentReport
    {
        [BsonId]
        [BsonRepresentation(BsonType.ObjectId)]
        public string Id { get; set; } = ObjectId.GenerateNewId().ToString();

        [BsonRepresentation(BsonType.String)]
        public Guid ReporterId { get; set; }
        
        public string ReporterName { get; set; } = string.Empty;

        public ReportTargetType TargetType { get; set; }

        [BsonRepresentation(BsonType.ObjectId)]
        public string? TargetPostId { get; set; }
        
        [BsonRepresentation(BsonType.ObjectId)]
        public string? TargetCommentId { get; set; }
        
        [BsonRepresentation(BsonType.String)]
        public Guid? TargetUserId { get; set; }

        public ReportReason Reason { get; set; }
        public string? AdditionalDetails { get; set; }

        // Review status
        public ReportStatus Status { get; set; } = ReportStatus.Pending;
        public string? ReviewNote { get; set; }
        
        [BsonRepresentation(BsonType.String)]
        public Guid? ReviewedByAdminId { get; set; }
        
        public DateTime? ReviewedAt { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }

    public enum ReportTargetType
    {
        Post,
        Comment,
        User,
        Message
    }

    public enum ReportReason
    {
        Spam,
        Harassment,
        HateSpeech,
        Violence,
        Nudity,
        FalseInformation,
        Scam,
        Other
    }

    public enum ReportStatus
    {
        Pending,
        UnderReview,
        ActionTaken,
        Dismissed
    }
}
