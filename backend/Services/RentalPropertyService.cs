using Microsoft.Extensions.Configuration;
using MongoDB.Driver;
using ServConnect.Models;

namespace ServConnect.Services
{
    public interface IRentalPropertyService
    {
        Task<RentalProperty> CreateAsync(RentalProperty property);
        Task<List<RentalProperty>> GetAllAvailableAsync(int skip = 0, int take = 20);
        Task<List<RentalProperty>> SearchAsync(string? query, HouseType? houseType, FurnishingType? furnishing, 
            string? city, decimal? minRent, decimal? maxRent, List<string>? amenities, int skip = 0, int take = 20, string? excludeOwnerId = null);
        Task<RentalProperty?> GetByIdAsync(string id);
        Task<RentalProperty?> GetByPropertyIdAsync(string propertyId);
        Task<List<RentalProperty>> GetByOwnerAsync(string ownerId);
        Task<bool> UpdateAsync(string id, RentalProperty property);
        Task<bool> DeleteAsync(string id);
        Task<bool> TogglePauseAsync(string id);
        Task<bool> IncrementViewCountAsync(string id);
        Task<long> GetTotalCountAsync(string? excludeOwnerId = null);
        Task<string> GeneratePropertyIdAsync();
        
        // Inquiry methods
        Task<RentalInquiry> CreateInquiryAsync(RentalInquiry inquiry);
        Task<List<RentalInquiry>> GetInquiriesByPropertyAsync(string propertyId);
        Task<List<RentalInquiry>> GetInquiriesByOwnerAsync(string ownerId);
        Task<bool> MarkInquiryAsReadAsync(string inquiryId);
    }

    public class RentalPropertyService : IRentalPropertyService
    {
        private readonly IMongoCollection<RentalProperty> _properties;
        private readonly IMongoCollection<RentalInquiry> _inquiries;

        public RentalPropertyService(IConfiguration config)
        {
            var conn = config["MongoDB:ConnectionString"] ?? "mongodb://localhost:27017";
            var dbName = config["MongoDB:DatabaseName"] ?? "ServConnectDb";
            var client = new MongoClient(conn);
            var db = client.GetDatabase(dbName);
            _properties = db.GetCollection<RentalProperty>("RentalProperties");
            _inquiries = db.GetCollection<RentalInquiry>("RentalInquiries");

            // Create indexes
            var indexKeys = Builders<RentalProperty>.IndexKeys
                .Ascending(p => p.City)
                .Ascending(p => p.HouseType)
                .Ascending(p => p.IsAvailable)
                .Ascending(p => p.IsPaused);
            _properties.Indexes.CreateOneAsync(new CreateIndexModel<RentalProperty>(indexKeys));
        }

        public async Task<string> GeneratePropertyIdAsync()
        {
            var count = await _properties.CountDocumentsAsync(Builders<RentalProperty>.Filter.Empty);
            return $"RENT-{DateTime.UtcNow:yyyy}-{(count + 1):D4}";
        }

        public async Task<RentalProperty> CreateAsync(RentalProperty property)
        {
            property.PropertyId = await GeneratePropertyIdAsync();
            property.CreatedAt = DateTime.UtcNow;
            property.UpdatedAt = DateTime.UtcNow;
            await _properties.InsertOneAsync(property);
            return property;
        }

        public async Task<List<RentalProperty>> GetAllAvailableAsync(int skip = 0, int take = 20)
        {
            return await _properties
                .Find(p => p.IsAvailable && !p.IsPaused)
                .SortByDescending(p => p.CreatedAt)
                .Skip(skip)
                .Limit(take)
                .ToListAsync();
        }

        public async Task<List<RentalProperty>> SearchAsync(string? query, HouseType? houseType, FurnishingType? furnishing,
            string? city, decimal? minRent, decimal? maxRent, List<string>? amenities, int skip = 0, int take = 20, string? excludeOwnerId = null)
        {
            var builder = Builders<RentalProperty>.Filter;
            var filter = builder.Eq(p => p.IsAvailable, true) & builder.Eq(p => p.IsPaused, false);

            // Exclude owner's own properties from public listing
            if (!string.IsNullOrEmpty(excludeOwnerId))
            {
                filter &= builder.Ne(p => p.OwnerId, excludeOwnerId);
            }

            if (!string.IsNullOrWhiteSpace(query))
            {
                var queryFilter = builder.Or(
                    builder.Regex(p => p.Title, new MongoDB.Bson.BsonRegularExpression(query, "i")),
                    builder.Regex(p => p.Description, new MongoDB.Bson.BsonRegularExpression(query, "i")),
                    builder.Regex(p => p.Area, new MongoDB.Bson.BsonRegularExpression(query, "i")),
                    builder.Regex(p => p.FullAddress, new MongoDB.Bson.BsonRegularExpression(query, "i"))
                );
                filter &= queryFilter;
            }

            if (houseType.HasValue)
            {
                filter &= builder.Eq(p => p.HouseType, houseType.Value);
            }

            if (furnishing.HasValue)
            {
                filter &= builder.Eq(p => p.Furnishing, furnishing.Value);
            }

            if (!string.IsNullOrWhiteSpace(city))
            {
                filter &= builder.Regex(p => p.City, new MongoDB.Bson.BsonRegularExpression(city, "i"));
            }

            if (minRent.HasValue)
            {
                filter &= builder.Gte(p => p.RentAmount, minRent.Value);
            }

            if (maxRent.HasValue)
            {
                filter &= builder.Lte(p => p.RentAmount, maxRent.Value);
            }

            if (amenities != null && amenities.Any())
            {
                filter &= builder.All(p => p.Amenities, amenities);
            }

            return await _properties
                .Find(filter)
                .SortByDescending(p => p.CreatedAt)
                .Skip(skip)
                .Limit(take)
                .ToListAsync();
        }

        public async Task<RentalProperty?> GetByIdAsync(string id)
        {
            return await _properties.Find(p => p.Id == id).FirstOrDefaultAsync();
        }

        public async Task<RentalProperty?> GetByPropertyIdAsync(string propertyId)
        {
            return await _properties.Find(p => p.PropertyId == propertyId).FirstOrDefaultAsync();
        }

        public async Task<List<RentalProperty>> GetByOwnerAsync(string ownerId)
        {
            return await _properties
                .Find(p => p.OwnerId == ownerId)
                .SortByDescending(p => p.CreatedAt)
                .ToListAsync();
        }

        public async Task<bool> UpdateAsync(string id, RentalProperty property)
        {
            property.UpdatedAt = DateTime.UtcNow;
            var result = await _properties.ReplaceOneAsync(p => p.Id == id, property);
            return result.ModifiedCount > 0;
        }

        public async Task<bool> DeleteAsync(string id)
        {
            var result = await _properties.DeleteOneAsync(p => p.Id == id);
            return result.DeletedCount > 0;
        }

        public async Task<bool> TogglePauseAsync(string id)
        {
            var property = await GetByIdAsync(id);
            if (property == null) return false;

            var update = Builders<RentalProperty>.Update
                .Set(p => p.IsPaused, !property.IsPaused)
                .Set(p => p.UpdatedAt, DateTime.UtcNow);

            var result = await _properties.UpdateOneAsync(p => p.Id == id, update);
            return result.ModifiedCount > 0;
        }

        public async Task<bool> IncrementViewCountAsync(string id)
        {
            var update = Builders<RentalProperty>.Update.Inc(p => p.ViewCount, 1);
            var result = await _properties.UpdateOneAsync(p => p.Id == id, update);
            return result.ModifiedCount > 0;
        }

        public async Task<long> GetTotalCountAsync(string? excludeOwnerId = null)
        {
            var builder = Builders<RentalProperty>.Filter;
            var filter = builder.Eq(p => p.IsAvailable, true) & builder.Eq(p => p.IsPaused, false);
            
            if (!string.IsNullOrEmpty(excludeOwnerId))
            {
                filter &= builder.Ne(p => p.OwnerId, excludeOwnerId);
            }
            
            return await _properties.CountDocumentsAsync(filter);
        }

        // Inquiry methods
        public async Task<RentalInquiry> CreateInquiryAsync(RentalInquiry inquiry)
        {
            inquiry.InquiryDate = DateTime.UtcNow;
            await _inquiries.InsertOneAsync(inquiry);
            return inquiry;
        }

        public async Task<List<RentalInquiry>> GetInquiriesByPropertyAsync(string propertyId)
        {
            return await _inquiries
                .Find(i => i.PropertyId == propertyId)
                .SortByDescending(i => i.InquiryDate)
                .ToListAsync();
        }

        public async Task<List<RentalInquiry>> GetInquiriesByOwnerAsync(string ownerId)
        {
            // Get all property IDs owned by this owner
            var ownerProperties = await _properties.Find(p => p.OwnerId == ownerId).ToListAsync();
            var propertyIds = ownerProperties.Select(p => p.Id).ToList();

            return await _inquiries
                .Find(i => propertyIds.Contains(i.PropertyId))
                .SortByDescending(i => i.InquiryDate)
                .ToListAsync();
        }

        public async Task<bool> MarkInquiryAsReadAsync(string inquiryId)
        {
            var update = Builders<RentalInquiry>.Update.Set(i => i.IsRead, true);
            var result = await _inquiries.UpdateOneAsync(i => i.Id == inquiryId, update);
            return result.ModifiedCount > 0;
        }
    }
}
