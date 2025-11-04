using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;
using System;

namespace ServConnect.Models
{
    public class ProductVariant
    {
        [BsonId]
        [BsonRepresentation(BsonType.ObjectId)]
        public string? Id { get; set; }

        public string VariantName { get; set; } = string.Empty; // e.g., "Color", "Size", "Flavor"

        public List<string> VariantValues { get; set; } = new(); // e.g., ["Red", "Blue", "Green"] for Color

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
}