using System.ComponentModel.DataAnnotations;
using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace ServConnect.Models
{
    public class RentalSubscription
    {
        [BsonId]
        [BsonRepresentation(BsonType.ObjectId)]
        public string Id { get; set; } = string.Empty;

        [Required]
        public string UserId { get; set; } = string.Empty;

        [Required]
        public RentalSubscriptionPlan Plan { get; set; }

        [BsonRepresentation(BsonType.Decimal128)]
        public decimal AmountPaid { get; set; }

        public string? RazorpayPaymentId { get; set; }
        public string? RazorpayOrderId { get; set; }

        public RentalPaymentStatus PaymentStatus { get; set; } = RentalPaymentStatus.Pending;

        [BsonDateTimeOptions(Kind = DateTimeKind.Utc)]
        public DateTime StartDate { get; set; }

        [BsonDateTimeOptions(Kind = DateTimeKind.Utc)]
        public DateTime ExpiryDate { get; set; }

        [BsonDateTimeOptions(Kind = DateTimeKind.Utc)]
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public bool IsActive => PaymentStatus == RentalPaymentStatus.Completed && ExpiryDate > DateTime.UtcNow;

        public bool IsExpired => ExpiryDate <= DateTime.UtcNow;
    }

    public enum RentalSubscriptionPlan
    {
        [Display(Name = "1 Week")]
        OneWeek = 1,
        [Display(Name = "1 Month")]
        OneMonth = 2,
        [Display(Name = "3 Months")]
        ThreeMonths = 3
    }

    public enum RentalPaymentStatus
    {
        Pending = 0,
        Completed = 1,
        Failed = 2,
        Cancelled = 3,
        Refunded = 4
    }

    // DTO for creating subscription
    public class CreateRentalSubscriptionDto
    {
        public RentalSubscriptionPlan Plan { get; set; }
    }

    // Response DTO with subscription details
    public class RentalSubscriptionStatusDto
    {
        public bool HasActiveSubscription { get; set; }
        public RentalSubscriptionPlan? CurrentPlan { get; set; }
        public DateTime? ExpiryDate { get; set; }
        public int? DaysRemaining { get; set; }
    }

    // Subscription plan pricing configuration
    public static class RentalSubscriptionPricing
    {
        public static readonly Dictionary<RentalSubscriptionPlan, decimal> Prices = new()
        {
            { RentalSubscriptionPlan.OneWeek, 49m },
            { RentalSubscriptionPlan.OneMonth, 149m },
            { RentalSubscriptionPlan.ThreeMonths, 349m }
        };

        public static readonly Dictionary<RentalSubscriptionPlan, int> DurationDays = new()
        {
            { RentalSubscriptionPlan.OneWeek, 7 },
            { RentalSubscriptionPlan.OneMonth, 30 },
            { RentalSubscriptionPlan.ThreeMonths, 90 }
        };

        public static readonly Dictionary<RentalSubscriptionPlan, string> PlanNames = new()
        {
            { RentalSubscriptionPlan.OneWeek, "1 Week Access" },
            { RentalSubscriptionPlan.OneMonth, "1 Month Access" },
            { RentalSubscriptionPlan.ThreeMonths, "3 Months Access" }
        };

        public static decimal GetPrice(RentalSubscriptionPlan plan) => Prices[plan];
        public static int GetDurationDays(RentalSubscriptionPlan plan) => DurationDays[plan];
        public static string GetPlanName(RentalSubscriptionPlan plan) => PlanNames[plan];
    }
}
