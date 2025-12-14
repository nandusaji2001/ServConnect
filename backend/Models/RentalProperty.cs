using System.ComponentModel.DataAnnotations;
using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace ServConnect.Models
{
    public class RentalProperty
    {
        [BsonId]
        [BsonRepresentation(BsonType.ObjectId)]
        public string Id { get; set; } = string.Empty;

        [Required]
        public string PropertyId { get; set; } = string.Empty; // Unique property ID like "RENT-2024-001"

        [Required]
        [StringLength(200)]
        public string Title { get; set; } = string.Empty;

        [StringLength(2000)]
        public string Description { get; set; } = string.Empty;

        [Required]
        public HouseType HouseType { get; set; }

        [Required]
        [BsonRepresentation(BsonType.Decimal128)]
        public decimal RentAmount { get; set; }

        [BsonRepresentation(BsonType.Decimal128)]
        public decimal DepositAmount { get; set; }

        [Required]
        [StringLength(500)]
        public string FullAddress { get; set; } = string.Empty;

        [StringLength(100)]
        public string City { get; set; } = string.Empty;

        [StringLength(100)]
        public string Area { get; set; } = string.Empty;

        [StringLength(20)]
        public string Pincode { get; set; } = string.Empty;

        public double? Latitude { get; set; }
        public double? Longitude { get; set; }

        // Furnishing
        public FurnishingType Furnishing { get; set; }

        // Amenities stored as list
        public List<string> Amenities { get; set; } = new();

        // Images stored as list of URLs
        public List<string> ImageUrls { get; set; } = new();

        public bool IsAvailable { get; set; } = true;
        public bool IsPaused { get; set; } = false;

        // Owner details
        [Required]
        public string OwnerId { get; set; } = string.Empty;

        [StringLength(100)]
        public string OwnerName { get; set; } = string.Empty;

        [StringLength(20)]
        public string OwnerPhone { get; set; } = string.Empty;

        [StringLength(100)]
        public string OwnerEmail { get; set; } = string.Empty;

        // Additional details
        public int? Bedrooms { get; set; }
        public int? Bathrooms { get; set; }
        public int? SquareFeet { get; set; }
        public int? FloorNumber { get; set; }
        public int? TotalFloors { get; set; }

        public bool PetsAllowed { get; set; } = false;
        public bool BachelorsAllowed { get; set; } = true;

        [BsonDateTimeOptions(Kind = DateTimeKind.Utc)]
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        [BsonDateTimeOptions(Kind = DateTimeKind.Utc)]
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

        public int ViewCount { get; set; } = 0;
    }

    public enum HouseType
    {
        [Display(Name = "1 BHK")]
        OneBHK = 1,
        [Display(Name = "2 BHK")]
        TwoBHK = 2,
        [Display(Name = "3 BHK")]
        ThreeBHK = 3,
        [Display(Name = "4+ BHK")]
        FourPlusBHK = 4,
        [Display(Name = "Studio")]
        Studio = 5,
        [Display(Name = "PG / Hostel")]
        PGHostel = 6,
        [Display(Name = "Villa")]
        Villa = 7,
        [Display(Name = "Independent House")]
        IndependentHouse = 8,
        [Display(Name = "Single Room")]
        SingleRoom = 9,
        [Display(Name = "Shared Room")]
        SharedRoom = 10
    }

    public enum FurnishingType
    {
        [Display(Name = "Unfurnished")]
        Unfurnished = 0,
        [Display(Name = "Semi-Furnished")]
        SemiFurnished = 1,
        [Display(Name = "Fully Furnished")]
        FullyFurnished = 2
    }

    // DTO for creating/updating rental property
    public class RentalPropertyDto
    {
        public string Title { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public HouseType HouseType { get; set; }
        public decimal RentAmount { get; set; }
        public decimal DepositAmount { get; set; }
        public string FullAddress { get; set; } = string.Empty;
        public string City { get; set; } = string.Empty;
        public string Area { get; set; } = string.Empty;
        public string Pincode { get; set; } = string.Empty;
        public double? Latitude { get; set; }
        public double? Longitude { get; set; }
        public FurnishingType Furnishing { get; set; }
        public List<string> Amenities { get; set; } = new();
        public List<string> ImageUrls { get; set; } = new();
        public bool IsAvailable { get; set; } = true;
        public string OwnerName { get; set; } = string.Empty;
        public string OwnerPhone { get; set; } = string.Empty;
        public string OwnerEmail { get; set; } = string.Empty;
        public int? Bedrooms { get; set; }
        public int? Bathrooms { get; set; }
        public int? SquareFeet { get; set; }
        public int? FloorNumber { get; set; }
        public int? TotalFloors { get; set; }
        public bool PetsAllowed { get; set; }
        public bool BachelorsAllowed { get; set; } = true;
    }

    // Rental Inquiry model
    public class RentalInquiry
    {
        [BsonId]
        [BsonRepresentation(BsonType.ObjectId)]
        public string Id { get; set; } = string.Empty;

        public string PropertyId { get; set; } = string.Empty;

        public string UserId { get; set; } = string.Empty;

        [StringLength(100)]
        public string UserName { get; set; } = string.Empty;

        [StringLength(20)]
        public string UserPhone { get; set; } = string.Empty;

        [StringLength(100)]
        public string UserEmail { get; set; } = string.Empty;

        [StringLength(1000)]
        public string Message { get; set; } = string.Empty;

        [BsonDateTimeOptions(Kind = DateTimeKind.Utc)]
        public DateTime InquiryDate { get; set; } = DateTime.UtcNow;

        public bool IsRead { get; set; } = false;
    }
}
