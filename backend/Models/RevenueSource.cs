using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace ServConnect.Models
{
    public enum RevenueType
    {
        ServicePublication,
        AdvertisementPayment,
        BookingCommission,
        Other
    }

    public class RevenueSource
    {
        [BsonId]
        [BsonRepresentation(BsonType.ObjectId)]
        public string? Id { get; set; }

        public RevenueType Type { get; set; }
        public decimal Amount { get; set; }
        public string Currency { get; set; } = "INR";
        public string Description { get; set; } = string.Empty;
        
        // Source reference details
        public string? SourceId { get; set; } // Payment ID, Advertisement ID, etc.
        public string? SourceType { get; set; } // "ServicePayment", "AdvertisementRequest", "BookingPayment"
        
        // User details
        [BsonRepresentation(BsonType.String)]
        public Guid? UserId { get; set; }
        public string? UserName { get; set; }
        public string? UserEmail { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    }

    public class RevenueAnalytics
    {
        public decimal TotalRevenue { get; set; }
        public decimal ServicePublicationRevenue { get; set; }
        public decimal AdvertisementRevenue { get; set; }
        public decimal BookingCommissionRevenue { get; set; }
        public decimal OtherRevenue { get; set; }
        
        public Dictionary<string, decimal> MonthlyRevenue { get; set; } = new();
        public Dictionary<RevenueType, decimal> RevenueByType { get; set; } = new();
        
        public DateTime FromDate { get; set; }
        public DateTime ToDate { get; set; }
        public int TotalTransactions { get; set; }
        public decimal AverageTransactionValue { get; set; }
    }

    public class RevenuePrediction
    {
        public DateTime PredictionDate { get; set; }
        public decimal PredictedAmount { get; set; }
        public double ConfidenceScore { get; set; }
        public string Period { get; set; } = string.Empty; // "1 Month", "3 Months", etc.
        public Dictionary<string, object> ModelFeatures { get; set; } = new();
    }
}
