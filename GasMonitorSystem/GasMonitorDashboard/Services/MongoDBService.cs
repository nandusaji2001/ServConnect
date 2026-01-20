using Microsoft.Extensions.Options;
using MongoDB.Driver;
using GasMonitorDashboard.Models;

namespace GasMonitorDashboard.Services;

public class MongoDBService
{
    private readonly IMongoCollection<WeightReading> _weightReadingsCollection;

    public MongoDBService(IOptions<MongoDBSettings> mongoDBSettings)
    {
        var mongoClient = new MongoClient(mongoDBSettings.Value.ConnectionString);
        var mongoDatabase = mongoClient.GetDatabase(mongoDBSettings.Value.DatabaseName);
        _weightReadingsCollection = mongoDatabase.GetCollection<WeightReading>(mongoDBSettings.Value.CollectionName);
    }

    public async Task<List<WeightReading>> GetAsync() =>
        await _weightReadingsCollection.Find(_ => true).ToListAsync();

    public async Task<WeightReading?> GetAsync(string id) =>
        await _weightReadingsCollection.Find(x => x.Id.ToString() == id).FirstOrDefaultAsync();

    public async Task CreateAsync(WeightReading newReading) =>
        await _weightReadingsCollection.InsertOneAsync(newReading);

    public async Task UpdateAsync(string id, WeightReading updatedReading) =>
        await _weightReadingsCollection.ReplaceOneAsync(x => x.Id.ToString() == id, updatedReading);

    public async Task RemoveAsync(string id) =>
        await _weightReadingsCollection.DeleteOneAsync(x => x.Id.ToString() == id);

    public async Task<List<WeightReading>> GetRecentAsync(int count = 10) =>
        await _weightReadingsCollection
            .Find(_ => true)
            .SortByDescending(x => x.Timestamp)
            .Limit(count)
            .ToListAsync();

    public async Task<WeightReading?> GetLatestAsync() =>
        await _weightReadingsCollection
            .Find(_ => true)
            .SortByDescending(x => x.Timestamp)
            .FirstOrDefaultAsync();
}
