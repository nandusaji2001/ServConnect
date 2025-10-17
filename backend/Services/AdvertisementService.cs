using Microsoft.Extensions.Configuration;
using MongoDB.Driver;
using ServConnect.Models;

namespace ServConnect.Services
{
    public class AdvertisementService : IAdvertisementService
    {
        private readonly IMongoCollection<Advertisement> _ads;

        public AdvertisementService(IConfiguration config)
        {
            var conn = config["MongoDB:ConnectionString"] ?? "mongodb://localhost:27017";
            var dbName = config["MongoDB:DatabaseName"] ?? "ServConnectDb";
            var client = new MongoClient(conn);
            var db = client.GetDatabase(dbName);
            _ads = db.GetCollection<Advertisement>("Advertisements");
        }

        public async Task<Advertisement> CreateAsync(Advertisement ad)
        {
            await _ads.InsertOneAsync(ad);
            return ad;
        }

        public async Task<List<Advertisement>> GetAllAsync()
        {
            return await _ads.Find(Builders<Advertisement>.Filter.Empty)
                             .SortByDescending(a => a.CreatedAt)
                             .ToListAsync();
        }

        public async Task<Advertisement?> GetLatestActiveAsync()
        {
            return await _ads.Find(a => a.IsActive)
                             .SortByDescending(a => a.CreatedAt)
                             .FirstOrDefaultAsync();
        }

        public async Task<List<Advertisement>> GetActiveAsync(int take = 10)
        {
            return await _ads.Find(a => a.IsActive)
                             .SortByDescending(a => a.CreatedAt)
                             .Limit(take)
                             .ToListAsync();
        }

        public async Task<Advertisement?> GetLatestActiveByTypeAsync(AdvertisementType type)
        {
            return await _ads.Find(a => a.IsActive && a.Type == type)
                             .SortByDescending(a => a.CreatedAt)
                             .FirstOrDefaultAsync();
        }

        public async Task<List<Advertisement>> GetActiveByTypeAsync(AdvertisementType type, int take = 10)
        {
            return await _ads.Find(a => a.IsActive && a.Type == type)
                             .SortByDescending(a => a.CreatedAt)
                             .Limit(take)
                             .ToListAsync();
        }

        public async Task<bool> DeleteAsync(string id)
        {
            var res = await _ads.DeleteOneAsync(a => a.Id == id);
            return res.DeletedCount == 1;
        }
    }
}