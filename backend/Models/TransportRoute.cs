using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace ServConnect.Models
{
    // Transport Types
    public static class TransportTypes
    {
        public const string Bus = "Bus";
        public const string Metro = "Metro";
        public const string Train = "Train";
        public const string Auto = "Auto";
        public const string Ferry = "Ferry";
        public const string Shuttle = "Shuttle";

        public static readonly string[] All = { Bus, Metro, Train, Auto, Ferry, Shuttle };
    }

    // Route Status
    public static class TransportRouteStatus
    {
        public const string Active = "Active";
        public const string Suspended = "Suspended";       // Temporarily suspended
        public const string Removed = "Removed";           // Removed due to downvotes
        public const string UnderReview = "UnderReview";   // Flagged for review
    }

    // Service Days
    public static class ServiceDays
    {
        public const string Daily = "Daily";
        public const string Weekdays = "Weekdays";
        public const string Weekends = "Weekends";
        public const string MondayToSaturday = "Monday to Saturday";
        public const string SundayOnly = "Sunday Only";

        public static readonly string[] All = { Daily, Weekdays, Weekends, MondayToSaturday, SundayOnly };
    }

    // Frequency Types
    public static class FrequencyTypes
    {
        public const string Every15Min = "Every 15 minutes";
        public const string Every30Min = "Every 30 minutes";
        public const string EveryHour = "Every hour";
        public const string Every2Hours = "Every 2 hours";
        public const string Morning = "Morning only";
        public const string Evening = "Evening only";
        public const string PeakHours = "Peak hours only";
        public const string LimitedService = "Limited service";

        public static readonly string[] All = { Every15Min, Every30Min, EveryHour, Every2Hours, Morning, Evening, PeakHours, LimitedService };
    }

    /// <summary>
    /// Transport Route - User-contributed transportation information with voting system
    /// </summary>
    [BsonIgnoreExtraElements]
    public class TransportRoute
    {
        [BsonId]
        [BsonRepresentation(BsonType.ObjectId)]
        public string Id { get; set; } = string.Empty;

        // Route Information
        public string TransportName { get; set; } = string.Empty;      // Bus name, Train name, etc.
        public string RouteNumber { get; set; } = string.Empty;        // Route/Service number
        public string TransportType { get; set; } = TransportTypes.Bus;

        // Locations
        public string StartLocation { get; set; } = string.Empty;
        public string StartLocationDetails { get; set; } = string.Empty;  // Stop name, landmark
        public string EndLocation { get; set; } = string.Empty;
        public string EndLocationDetails { get; set; } = string.Empty;    // Stop name, landmark
        
        // Intermediate Stops (major stops along the route)
        public List<string> IntermediateStops { get; set; } = new();

        // Timings
        public string DepartureTime { get; set; } = string.Empty;      // Format: HH:mm
        public string ArrivalTime { get; set; } = string.Empty;        // Format: HH:mm
        public string Duration { get; set; } = string.Empty;           // Estimated duration
        public string ServiceDays { get; set; } = Models.ServiceDays.Daily;
        public string Frequency { get; set; } = string.Empty;          // How often it runs

        // Fare Information
        public decimal? ApproxFare { get; set; }                       // Approximate fare
        public string? FareDetails { get; set; }                       // Additional fare info

        // Additional Features
        public bool IsACAvailable { get; set; }
        public bool IsWheelchairAccessible { get; set; }
        public bool HasWifi { get; set; }
        public bool IsExpressService { get; set; }                     // Non-stop or limited stops
        public bool IsPeakHourService { get; set; }                    // Runs only during peak hours
        public string? AdditionalNotes { get; set; }                   // Any other useful info

        // District (location-based filtering)
        public string District { get; set; } = KeralaDistricts.Idukki;

        // Voting System
        public int Upvotes { get; set; }
        public int Downvotes { get; set; }
        public List<Guid> UpvotedBy { get; set; } = new();
        public List<Guid> DownvotedBy { get; set; } = new();

        // Auto-removal threshold settings
        public const int RemovalThreshold = 5;  // Remove if (Downvotes - Upvotes) >= this value

        // Status
        public string Status { get; set; } = TransportRouteStatus.Active;

        // Contributor Information
        public Guid ContributorId { get; set; }
        public string ContributorName { get; set; } = string.Empty;
        public bool IsVerifiedContributor { get; set; }                // Badge for trusted contributors

        // Timestamps
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
        public DateTime? LastConfirmedAt { get; set; }                 // Last time someone confirmed it's accurate

        // Computed property for reliability score
        [BsonIgnore]
        public int Score => Upvotes - Downvotes;

        [BsonIgnore]
        public double ReliabilityPercentage
        {
            get
            {
                var total = Upvotes + Downvotes;
                if (total == 0) return 100;
                return Math.Round((double)Upvotes / total * 100, 1);
            }
        }
    }

    /// <summary>
    /// Route Update/Correction Request - For reporting changes or issues
    /// </summary>
    [BsonIgnoreExtraElements]
    public class RouteUpdateRequest
    {
        [BsonId]
        [BsonRepresentation(BsonType.ObjectId)]
        public string Id { get; set; } = string.Empty;

        public string RouteId { get; set; } = string.Empty;

        // Update Type
        public string UpdateType { get; set; } = string.Empty;         // TimingChange, RouteChange, ServiceStopped, etc.
        public string Description { get; set; } = string.Empty;
        public string? ProposedChange { get; set; }

        // Reporter
        public Guid ReporterId { get; set; }
        public string ReporterName { get; set; } = string.Empty;

        // Status
        public bool IsResolved { get; set; }
        public int SupportingVotes { get; set; }                       // Others agreeing with this report
        public List<Guid> SupportedBy { get; set; } = new();

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime? ResolvedAt { get; set; }

        [BsonIgnore]
        public TransportRoute? Route { get; set; }
    }

    /// <summary>
    /// User's saved/favorite routes
    /// </summary>
    [BsonIgnoreExtraElements]
    public class SavedRoute
    {
        [BsonId]
        [BsonRepresentation(BsonType.ObjectId)]
        public string Id { get; set; } = string.Empty;

        public Guid UserId { get; set; }
        public string RouteId { get; set; } = string.Empty;
        public string? CustomLabel { get; set; }                       // e.g., "Daily commute", "Office route"
        public bool NotifyOnChanges { get; set; }                      // Alert if route info changes

        public DateTime SavedAt { get; set; } = DateTime.UtcNow;

        [BsonIgnore]
        public TransportRoute? Route { get; set; }
    }
}
