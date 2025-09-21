using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using MongoDB.Driver;
using MongoDB.Bson;

namespace ServConnect.Services
{
    public class RatingService : IRatingService
    {
        private readonly IMongoCollection<UserRating> _ratings;

        public RatingService(IConfiguration config)
        {
            var conn = config["MongoDB:ConnectionString"] ?? "mongodb://localhost:27017";
            var dbName = config["MongoDB:DatabaseName"] ?? "ServConnectDb";
            var client = new MongoClient(conn);
            var db = client.GetDatabase(dbName);
            _ratings = db.GetCollection<UserRating>("UserRatings");
            _ratings.Indexes.CreateOne(new CreateIndexModel<UserRating>(Builders<UserRating>.IndexKeys.Ascending(x => x.ServiceKey).Ascending(x => x.UserId), new CreateIndexOptions { Unique = true }));
        }

        public string ComposeKey(string source, string id)
        {
            return $"{source}:{id}"; // e.g., osm:12345 or mongo:60fd...
        }

        public string DetectKeyFrom(string? id, string? mapUrl)
        {
            if (!string.IsNullOrWhiteSpace(id) && id!.Length == 24)
                return ComposeKey("mongo", id);
            // If OSM, try to derive a stable key from mapUrl (contains lat/lon). Fallback to id if present.
            if (!string.IsNullOrWhiteSpace(id)) return ComposeKey("ext", id);
            return $"url:{(mapUrl ?? string.Empty)}";
        }

        public async Task SubmitAsync(string userId, string serviceKey, int rating)
        {
            rating = Math.Max(1, Math.Min(5, rating));
            var filter = Builders<UserRating>.Filter.Where(x => x.ServiceKey == serviceKey && x.UserId == userId);
            var update = Builders<UserRating>.Update
                .Set(x => x.Rating, rating)
                .Set(x => x.UpdatedAt, DateTime.UtcNow)
                .SetOnInsert(x => x.CreatedAt, DateTime.UtcNow)
                .SetOnInsert(x => x.ServiceKey, serviceKey)
                .SetOnInsert(x => x.UserId, userId);
            await _ratings.UpdateOneAsync(filter, update, new UpdateOptions { IsUpsert = true });
        }

        public async Task<Dictionary<string, (decimal average, int count)>> GetAveragesAsync(IEnumerable<string> serviceKeys)
        {
            var keys = serviceKeys.Distinct().ToArray();
            if (keys.Length == 0) return new Dictionary<string, (decimal, int)>();

            var match = Builders<UserRating>.Filter.In(x => x.ServiceKey, keys);
            var group = new BsonDocument
            {
                { "_id", "$ServiceKey" },
                { "avg", new BsonDocument("$avg", "$Rating") },
                { "count", new BsonDocument("$sum", 1) }
            };
            var pipeline = new[]
            {
                new BsonDocument("$match", match.Render(_ratings.DocumentSerializer, _ratings.Settings.SerializerRegistry)),
                new BsonDocument("$group", group)
            };
            var results = await _ratings.Aggregate<BsonDocument>(pipeline).ToListAsync();
            var dict = new Dictionary<string, (decimal, int)>();
            foreach (var doc in results)
            {
                var key = doc["_id"].AsString;
                var avg = doc["avg"].ToDecimal();
                var cnt = doc["count"].ToInt32();
                dict[key] = (avg, cnt);
            }
            return dict;
        }

        private class UserRating
        {
            public string Id { get; set; } = Guid.NewGuid().ToString("n");
            public string ServiceKey { get; set; } = string.Empty; // osm:..., mongo:...
            public string UserId { get; set; } = string.Empty;
            public int Rating { get; set; }
            public DateTime CreatedAt { get; set; }
            public DateTime UpdatedAt { get; set; }
        }
    }
}