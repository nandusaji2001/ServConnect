using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace ServConnect.Models
{
    public class ServicePublicationPlan
    {
        [BsonId]
        [BsonRepresentation(BsonType.ObjectId)]
        public string? Id { get; set; }

        public string Name { get; set; } = string.Empty;
        public int DurationInMonths { get; set; }
        public decimal PriceInRupees { get; set; }
        public string Description { get; set; } = string.Empty;
        public bool IsActive { get; set; } = true;
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        // Static method to get predefined plans
        public static List<ServicePublicationPlan> GetPredefinedPlans()
        {
            return new List<ServicePublicationPlan>
            {
                new ServicePublicationPlan
                {
                    Name = "1 Month",
                    DurationInMonths = 1,
                    PriceInRupees = 49,
                    Description = "Publish your service for 1 month"
                },
                new ServicePublicationPlan
                {
                    Name = "3 Months",
                    DurationInMonths = 3,
                    PriceInRupees = 119,
                    Description = "Publish your service for 3 months (Save ₹28)"
                },
                new ServicePublicationPlan
                {
                    Name = "6 Months",
                    DurationInMonths = 6,
                    PriceInRupees = 199,
                    Description = "Publish your service for 6 months (Save ₹95)"
                },
                new ServicePublicationPlan
                {
                    Name = "1 Year",
                    DurationInMonths = 12,
                    PriceInRupees = 349,
                    Description = "Publish your service for 1 year (Save ₹239)"
                },
                new ServicePublicationPlan
                {
                    Name = "3 Years",
                    DurationInMonths = 36,
                    PriceInRupees = 799,
                    Description = "Publish your service for 3 years (Save ₹968)"
                }
            };
        }
    }
}
