using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace ServConnect.Models
{
    public class UserNotification
    {
        [BsonId]
        [BsonRepresentation(BsonType.ObjectId)]
        public string? Id { get; set; }

        public string UserId { get; set; } = string.Empty;
        public string Title { get; set; } = string.Empty;
        public string Message { get; set; } = string.Empty;
        public UserNotificationType Type { get; set; } = UserNotificationType.Info;
        public string? RelatedEntityId { get; set; }
        public string? RelatedEntityType { get; set; }
        public string? ActionUrl { get; set; }
        public bool IsRead { get; set; } = false;
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        // For refund notifications
        public decimal? RefundAmount { get; set; }
        public string? RefundStatus { get; set; }
    }

    public enum UserNotificationType
    {
        Info,
        Success,
        Warning,
        Error,
        EventCancelled,
        RefundInitiated,
        RefundCompleted,
        TicketConfirmed
    }
}
