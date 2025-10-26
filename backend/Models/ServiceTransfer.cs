using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace ServConnect.Models
{
    public enum TransferStatus
    {
        PendingUserApproval = 0,    // Waiting for user to approve/reject
        PendingProviderAcceptance = 1, // User approved, waiting for new provider to accept/reject
        Approved = 2,               // Both user and new provider approved
        RejectedByUser = 3,         // User rejected the transfer
        RejectedByProvider = 4,     // New provider rejected the transfer
        Cancelled = 5               // Original provider cancelled the request
    }

    public class ServiceTransfer
    {
        [BsonId]
        [BsonRepresentation(BsonType.ObjectId)]
        public string? Id { get; set; }

        // Related booking
        [BsonRepresentation(BsonType.ObjectId)]
        public string BookingId { get; set; } = string.Empty;

        // Original provider (who wants to transfer)
        [BsonRepresentation(BsonType.String)]
        public Guid OriginalProviderId { get; set; }
        public string OriginalProviderName { get; set; } = string.Empty;

        // New provider (who will receive the service)
        [BsonRepresentation(BsonType.String)]
        public Guid NewProviderId { get; set; }
        public string NewProviderName { get; set; } = string.Empty;

        // User who booked the service
        [BsonRepresentation(BsonType.String)]
        public Guid UserId { get; set; }
        public string UserName { get; set; } = string.Empty;

        // Service details
        public string ServiceName { get; set; } = string.Empty;
        public DateTime ServiceDateTime { get; set; }

        // Transfer request details
        public string? TransferReason { get; set; }
        public DateTime RequestedAtUtc { get; set; } = DateTime.UtcNow;

        // Status tracking
        public TransferStatus Status { get; set; } = TransferStatus.PendingUserApproval;

        // User response
        public DateTime? UserRespondedAtUtc { get; set; }
        public string? UserMessage { get; set; }

        // New provider response
        public DateTime? ProviderRespondedAtUtc { get; set; }
        public string? ProviderMessage { get; set; }

        // Final transfer completion
        public DateTime? TransferCompletedAtUtc { get; set; }
    }
}
