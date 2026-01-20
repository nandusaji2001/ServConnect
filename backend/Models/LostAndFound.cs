using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace ServConnect.Models
{
    public static class LostFoundItemCategory
    {
        public const string Wallet = "Wallet";
        public const string Mobile = "Mobile";
        public const string IdCard = "ID Card";
        public const string Bag = "Bag";
        public const string Pet = "Pet";
        public const string Electronics = "Electronics";
        public const string Documents = "Documents";
        public const string Others = "Others";

        public static readonly string[] All = { Wallet, Mobile, IdCard, Bag, Pet, Electronics, Documents, Others };
    }

    public static class LostFoundItemCondition
    {
        public const string Good = "Good";
        public const string Damaged = "Damaged";
        public const string Used = "Used";

        public static readonly string[] All = { Good, Damaged, Used };
    }

    public static class LostFoundItemStatus
    {
        public const string Available = "Available";
        public const string ClaimPending = "ClaimPending";
        public const string Verified = "Verified";
        public const string Returned = "Returned";
    }

    public static class LostItemStatus
    {
        public const string Active = "Active";
        public const string FoundByOther = "FoundByOther";
        public const string Recovered = "Recovered";
        public const string Closed = "Closed";
    }

    public static class ClaimStatus
    {
        public const string Pending = "Pending";
        public const string Rejected = "Rejected";
        public const string RetryAllowed = "RetryAllowed";
        public const string Verified = "Verified";
        public const string Blocked = "Blocked";
    }

    [BsonIgnoreExtraElements]
    public class LostFoundItem
    {
        [BsonId]
        [BsonRepresentation(BsonType.ObjectId)]
        public string Id { get; set; } = string.Empty;

        // Public Item Information
        public string Title { get; set; } = string.Empty;
        public string Category { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public List<string> Images { get; set; } = new();
        public string Condition { get; set; } = string.Empty;

        // Found date & location
        public DateTime FoundDate { get; set; }
        public string FoundLocation { get; set; } = string.Empty;
        public string? FoundLocationDetails { get; set; }

        // Found User (auto-filled)
        public Guid FoundByUserId { get; set; }
        public string FoundByUserName { get; set; } = string.Empty;
        public string FoundByUserEmail { get; set; } = string.Empty;
        public string? FoundByUserPhone { get; set; }

        // Item Status
        public string Status { get; set; } = LostFoundItemStatus.Available;

        // Verified Claimant (set after verification)
        public Guid? VerifiedClaimantId { get; set; }
        public string? VerifiedClaimantName { get; set; }

        // Timestamps
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
        public DateTime? ReturnedAt { get; set; }

        // Navigation - Claims for this item
        [BsonIgnore]
        public List<ItemClaim> Claims { get; set; } = new();
    }

    /// <summary>
    /// Lost Item Report - Published by users who lost an item
    /// </summary>
    [BsonIgnoreExtraElements]
    public class LostItemReport
    {
        [BsonId]
        [BsonRepresentation(BsonType.ObjectId)]
        public string Id { get; set; } = string.Empty;

        // Item Information
        public string Title { get; set; } = string.Empty;
        public string Category { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public List<string> Images { get; set; } = new();

        // Lost date & location
        public DateTime LostDate { get; set; }
        public string LostLocation { get; set; } = string.Empty;
        public string? LostLocationDetails { get; set; }

        // Lost User (owner)
        public Guid LostByUserId { get; set; }
        public string LostByUserName { get; set; } = string.Empty;
        public string LostByUserEmail { get; set; } = string.Empty;
        public string? LostByUserPhone { get; set; }

        // Status
        public string Status { get; set; } = LostItemStatus.Active;

        // Found by someone (when marked as found)
        public Guid? FoundByUserId { get; set; }
        public string? FoundByUserName { get; set; }
        public string? FoundByUserEmail { get; set; }
        public string? FoundByUserPhone { get; set; }
        public string? FoundLocation { get; set; }
        public string? FoundNote { get; set; }
        public DateTime? FoundAt { get; set; }

        // Timestamps
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
        public DateTime? RecoveredAt { get; set; }
    }

    [BsonIgnoreExtraElements]
    public class ItemClaim
    {
        [BsonId]
        [BsonRepresentation(BsonType.ObjectId)]
        public string Id { get; set; } = string.Empty;

        // Reference to the item
        [BsonRepresentation(BsonType.ObjectId)]
        public string ItemId { get; set; } = string.Empty;

        // Claimant Info
        public Guid ClaimantId { get; set; }
        public string ClaimantName { get; set; } = string.Empty;
        public string ClaimantEmail { get; set; } = string.Empty;
        public string? ClaimantPhone { get; set; }

        // Private Ownership Details (secret info only owner would know)
        public string PrivateOwnershipDetails { get; set; } = string.Empty;

        // Optional proof uploads
        public List<string> ProofImages { get; set; } = new();

        // Claim Status
        public string Status { get; set; } = ClaimStatus.Pending;
        public int AttemptCount { get; set; } = 1;

        // Verification by Found User
        public bool? IsVerifiedByFoundUser { get; set; }
        public string? VerificationNote { get; set; }
        public DateTime? VerifiedAt { get; set; }

        // Timestamps
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

        // Navigation
        [BsonIgnore]
        public LostFoundItem? Item { get; set; }
    }
}
