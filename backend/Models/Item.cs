using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;
using System;

namespace ServConnect.Models
{
    public class Item
    {
        [BsonId]
        [BsonRepresentation(BsonType.ObjectId)]
        public string Id { get; set; } = null!;

        // Owner of the item (ServiceProvider or Vendor)
        [BsonRepresentation(BsonType.String)]
        public Guid OwnerId { get; set; }

        public string OwnerRole { get; set; } = RoleTypes.ServiceProvider; // or Vendor

        public string Title { get; set; } = string.Empty;
        public string? Description { get; set; }

        // Optional categorization / inventory fields
        public string? Category { get; set; }
        public string? SKU { get; set; }
        public int Stock { get; set; } = 0;

        // Image URL saved under wwwroot
        public string? ImageUrl { get; set; }

        [BsonRepresentation(BsonType.Decimal128)]
        public decimal Price { get; set; }

        public bool IsActive { get; set; } = true;

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    }
}