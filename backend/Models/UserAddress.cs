using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace ServConnect.Models
{
    public class UserAddress
    {
        [BsonId]
        [BsonRepresentation(BsonType.ObjectId)]
        public string? Id { get; set; }

        [BsonRepresentation(BsonType.String)]
        public Guid UserId { get; set; }

        public string Label { get; set; } = string.Empty; // e.g., "Home", "Office", "Other"
        public string FullName { get; set; } = string.Empty;
        public string PhoneNumber { get; set; } = string.Empty;
        public string AddressLine1 { get; set; } = string.Empty;
        public string? AddressLine2 { get; set; }
        public string City { get; set; } = string.Empty;
        public string State { get; set; } = string.Empty;
        public string PostalCode { get; set; } = string.Empty;
        public string Country { get; set; } = "India";
        public string? Landmark { get; set; }
        
        public bool IsDefault { get; set; } = false;
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

        // Computed property for display
        public string FormattedAddress => 
            $"{AddressLine1}" +
            (string.IsNullOrEmpty(AddressLine2) ? "" : $", {AddressLine2}") +
            (string.IsNullOrEmpty(Landmark) ? "" : $", Near {Landmark}") +
            $", {City}, {State} - {PostalCode}, {Country}";
    }
}
