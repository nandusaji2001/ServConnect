using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace ServConnect.Models.Community
{
    /// <summary>
    /// Extended community profile for users (linked to main Users model)
    /// </summary>
    public class CommunityProfile
    {
        [BsonId]
        [BsonRepresentation(BsonType.String)]
        public Guid UserId { get; set; }

        public string? Username { get; set; } // Unique handle like @username
        public string? Bio { get; set; }
        public string? Website { get; set; }
        public string? Location { get; set; }

        // Cover/Banner image
        public string? CoverImageUrl { get; set; }

        // Stats (denormalized for performance)
        public int PostsCount { get; set; } = 0;
        public int FollowersCount { get; set; } = 0;
        public int FollowingCount { get; set; } = 0;

        // Privacy settings
        public bool IsPrivate { get; set; } = false;
        public bool AllowMessages { get; set; } = true;
        public bool ShowActivity { get; set; } = true;

        // Preferences
        public string PreferredLanguage { get; set; } = "en";
        public string Theme { get; set; } = "light"; // light/dark

        // Account confirmation
        public bool HasConfirmedCommunityAccount { get; set; } = false;

        // Moderation & Bans
        public int ViolationCount { get; set; } = 0; // Total violations
        public int CurrentViolationStreak { get; set; } = 0; // Violations since last ban
        public bool IsBanned { get; set; } = false;
        public DateTime? BanExpiresAt { get; set; }
        public string? BanReason { get; set; }
        public int BanLevel { get; set; } = 0; // 0=none, 1=7days, 2=30days, 3=permanent
        public List<BanHistory> BanHistory { get; set; } = new();

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
        public DateTime? LastActiveAt { get; set; }
    }

    public class BanHistory
    {
        public DateTime BannedAt { get; set; }
        public DateTime? UnbannedAt { get; set; }
        public int DurationDays { get; set; }
        public string Reason { get; set; } = string.Empty;
        public int ViolationCount { get; set; }
    }
}
