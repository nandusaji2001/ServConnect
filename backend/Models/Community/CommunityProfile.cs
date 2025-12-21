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

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
        public DateTime? LastActiveAt { get; set; }
    }
}
