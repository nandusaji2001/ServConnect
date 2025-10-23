using System.ComponentModel.DataAnnotations;
using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace ServConnect.Models
{
    public class Notification
    {
        [BsonId]
        [BsonRepresentation(BsonType.ObjectId)]
        public string Id { get; set; } = ObjectId.GenerateNewId().ToString();
        
        [Required]
        public string UserId { get; set; } = string.Empty;
        
        [Required]
        public string Title { get; set; } = string.Empty;
        
        [Required]
        public string Message { get; set; } = string.Empty;
        
        [Required]
        public NotificationType Type { get; set; }
        
        public string? RelatedEntityId { get; set; }
        
        public bool IsRead { get; set; } = false;
        
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        
        public string? ActionUrl { get; set; }
    }
    
    public enum NotificationType
    {
        BookingReceived,
        BookingConfirmed,
        BookingCompleted,
        BookingCancelled,
        PaymentReceived,
        ReviewReceived,
        ServiceExpiring,
        ServiceExpired,
        ProfileUpdate,
        SystemAlert,
        General,
        ServiceOtp
    }
}
