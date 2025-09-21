using Microsoft.Extensions.Configuration;
using MongoDB.Driver;
using MongoDB.Bson; // for BsonRegularExpression
using ServConnect.Models;
using System.Text.RegularExpressions;

namespace ServConnect.Services
{
    public class LocalDirectory : ILocalDirectory
    {
        private readonly IMongoCollection<LocalCategory> _categories;
        private readonly IMongoCollection<LocalService> _services;

        public LocalDirectory(IConfiguration config)
        {
            var conn = config["MongoDB:ConnectionString"] ?? "mongodb://localhost:27017";
            var dbName = config["MongoDB:DatabaseName"] ?? "ServConnectDb";
            var client = new MongoClient(conn);
            var db = client.GetDatabase(dbName);
            _categories = db.GetCollection<LocalCategory>("LocalCategories");
            _services = db.GetCollection<LocalService>("LocalServices");
        }

        private static string ToSlug(string name)
        {
            var slug = name.Trim().ToLowerInvariant();
            slug = Regex.Replace(slug, "[^a-z0-9\\s-]", "");
            slug = Regex.Replace(slug, "\\s+", "-");
            slug = Regex.Replace(slug, "-+", "-");
            return slug;
        }

        public async Task<IReadOnlyList<LocalCategory>> GetCategoriesAsync()
        {
            var list = await _categories.Find(Builders<LocalCategory>.Filter.Empty)
                                        .SortBy(x => x.Name).ToListAsync();
            if (list.Count == 0)
            {
                // Seed some defaults: Hospitals, Police Stations, Petrol Pumps
                var defaults = new[] { "Hospitals", "Police Stations", "Petrol Pumps" };
                foreach (var n in defaults)
                    await EnsureCategoryAsync(n);
                list = await _categories.Find(Builders<LocalCategory>.Filter.Empty)
                                         .SortBy(x => x.Name).ToListAsync();
            }
            return list;
        }

        public async Task<LocalCategory> EnsureCategoryAsync(string name)
        {
            var slug = ToSlug(name);
            var existing = await _categories.Find(x => x.Slug == slug).FirstOrDefaultAsync();
            if (existing != null) return existing;
            var cat = new LocalCategory { Id = null!, Name = name.Trim(), Slug = slug, CreatedAt = DateTime.UtcNow };
            await _categories.InsertOneAsync(cat);
            return cat;
        }

        public async Task<LocalService> CreateServiceAsync(LocalService svc)
        {
            // ensure category exists
            var cat = await EnsureCategoryAsync(svc.CategoryName);
            svc.CategorySlug = cat.Slug;
            svc.CategoryName = cat.Name;
            svc.Id = null!;
            svc.CreatedAt = DateTime.UtcNow;
            await _services.InsertOneAsync(svc);
            return svc;
        }

        public async Task<bool> UpdateServiceAsync(LocalService svc)
        {
            // keep category normalized if name changed
            var cat = await EnsureCategoryAsync(svc.CategoryName);
            svc.CategorySlug = cat.Slug;
            svc.CategoryName = cat.Name;
            var res = await _services.ReplaceOneAsync(x => x.Id == svc.Id, svc);
            return res.ModifiedCount == 1;
        }

        public async Task<bool> DeleteServiceAsync(string id)
        {
            var res = await _services.DeleteOneAsync(x => x.Id == id);
            return res.DeletedCount == 1;
        }

        public async Task<LocalService?> GetServiceAsync(string id)
        {
            return await _services.Find(x => x.Id == id).FirstOrDefaultAsync();
        }

        public async Task<IReadOnlyList<LocalService>> GetByCategoryAsync(string categorySlug)
        {
            return await _services.Find(x => x.CategorySlug == categorySlug && x.IsActive)
                                  .SortBy(x => x.Name).ToListAsync();
        }

        public async Task<IReadOnlyList<LocalService>> SearchAsync(string? q, string? categorySlug, string? locationName)
        {
            var filter = Builders<LocalService>.Filter.Eq(x => x.IsActive, true);
            if (!string.IsNullOrWhiteSpace(categorySlug))
            {
                filter &= Builders<LocalService>.Filter.Eq(x => x.CategorySlug, categorySlug);
            }
            if (!string.IsNullOrWhiteSpace(q))
            {
                var regex = new BsonRegularExpression($".*{Regex.Escape(q.Trim())}.*", "i");
                var nameFilter = Builders<LocalService>.Filter.Regex(x => x.Name, regex);
                var addrFilter = Builders<LocalService>.Filter.Regex(x => x.Address!, regex);
                filter &= Builders<LocalService>.Filter.Or(nameFilter, addrFilter);
            }
            // locationName intentionally ignored in Mongo-backed search
            return await _services.Find(filter).SortBy(x => x.Name).ToListAsync();
        }
    }
}