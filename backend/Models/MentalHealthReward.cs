using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace ServConnect.Models
{
    /// <summary>
    /// Reward catalog item
    /// </summary>
    public class RewardItem
    {
        public string Name { get; set; } = string.Empty;
        public int StarsRequired { get; set; }
        public string ImageUrl { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
    }

    /// <summary>
    /// User's reward redemption request
    /// </summary>
    public class RewardRedemption
    {
        [BsonId]
        [BsonRepresentation(BsonType.ObjectId)]
        public string Id { get; set; } = ObjectId.GenerateNewId().ToString();

        [BsonElement("userId")]
        public string UserId { get; set; } = string.Empty;

        [BsonElement("userName")]
        public string UserName { get; set; } = string.Empty;

        [BsonElement("rewardName")]
        public string RewardName { get; set; } = string.Empty;

        [BsonElement("starsSpent")]
        public int StarsSpent { get; set; }

        [BsonElement("imageUrl")]
        public string ImageUrl { get; set; } = string.Empty;

        [BsonElement("recipientName")]
        public string RecipientName { get; set; } = string.Empty;

        [BsonElement("email")]
        public string Email { get; set; } = string.Empty;

        [BsonElement("phone")]
        public string Phone { get; set; } = string.Empty;

        [BsonElement("address")]
        public string Address { get; set; } = string.Empty;

        [BsonElement("status")]
        public string Status { get; set; } = "Pending"; // Pending, Processing, Shipped, Delivered

        [BsonElement("redeemedAt")]
        public DateTime RedeemedAt { get; set; } = DateTime.UtcNow;

        [BsonElement("updatedAt")]
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    }
}
