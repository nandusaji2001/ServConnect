using Microsoft.Extensions.Configuration;
using MongoDB.Driver;
using ServConnect.Models;

namespace ServConnect.Services
{
    public class ItemService : IItemService
    {
        private readonly IMongoCollection<Item> _items;

        public ItemService(IConfiguration config)
        {
            var conn = config["MongoDB:ConnectionString"] ?? "mongodb://localhost:27017";
            var dbName = config["MongoDB:DatabaseName"] ?? "ServConnectDb";
            var client = new MongoClient(conn);
            var db = client.GetDatabase(dbName);
            _items = db.GetCollection<Item>("Items");
        }

        public async Task<Item> CreateAsync(Item item)
        {
            await _items.InsertOneAsync(item);
            return item;
        }

        public async Task<Item?> GetByIdAsync(string id)
        {
            return await _items.Find(i => i.Id == id).FirstOrDefaultAsync();
        }

        public async Task<List<Item>> GetByOwnerAsync(Guid ownerId)
        {
            return await _items.Find(i => i.OwnerId == ownerId).ToListAsync();
        }

        public async Task<List<Item>> GetAllAsync(bool includeInactive = false)
        {
            var filter = includeInactive ? Builders<Item>.Filter.Empty : Builders<Item>.Filter.Eq(i => i.IsActive, true);
            return await _items.Find(filter).SortByDescending(i => i.CreatedAt).ToListAsync();
        }

        public async Task<bool> UpdateAsync(Item item)
        {
            item.UpdatedAt = DateTime.UtcNow;
            var res = await _items.ReplaceOneAsync(i => i.Id == item.Id && i.OwnerId == item.OwnerId, item);
            return res.ModifiedCount == 1;
        }

        public async Task<bool> DeleteAsync(string id)
        {
            var res = await _items.DeleteOneAsync(i => i.Id == id);
            return res.DeletedCount == 1;
        }
    }
}