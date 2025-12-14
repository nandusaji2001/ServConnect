using MongoDB.Driver;
using ServConnect.Models;

namespace ServConnect.Services
{
    public interface IRentalQueryService
    {
        Task<RentalQuery> CreateQueryAsync(RentalQuery query);
        Task<RentalQuery?> GetQueryByIdAsync(string queryId);
        Task<List<RentalQuery>> GetQueriesForOwnerAsync(string ownerId);
        Task<List<RentalQuery>> GetQueriesForUserAsync(string userId);
        Task<RentalQuery?> AddReplyAsync(string queryId, QueryMessage message, bool isOwnerReply);
        Task MarkAsReadByOwnerAsync(string queryId);
        Task MarkAsReadByUserAsync(string queryId);
        Task<int> GetUnreadCountForOwnerAsync(string ownerId);
        Task<int> GetUnreadCountForUserAsync(string userId);
        Task DeleteQueryAsync(string queryId);
    }

    public class RentalQueryService : IRentalQueryService
    {
        private readonly IMongoCollection<RentalQuery> _queries;

        public RentalQueryService(IConfiguration config)
        {
            var connectionString = config["MongoDB:ConnectionString"];
            var databaseName = config["MongoDB:DatabaseName"] ?? "ServConnectDb";

            var client = new MongoClient(connectionString);
            var database = client.GetDatabase(databaseName);
            _queries = database.GetCollection<RentalQuery>("RentalQueries");

            // Create TTL index for auto-deletion after 7 days
            var indexOptions = new CreateIndexOptions { ExpireAfter = TimeSpan.Zero };
            var indexModel = new CreateIndexModel<RentalQuery>(
                Builders<RentalQuery>.IndexKeys.Ascending(x => x.ExpireAt),
                indexOptions
            );
            
            try
            {
                _queries.Indexes.CreateOne(indexModel);
            }
            catch
            {
                // Index may already exist
            }
        }

        public async Task<RentalQuery> CreateQueryAsync(RentalQuery query)
        {
            query.CreatedAt = DateTime.UtcNow;
            query.LastUpdatedAt = DateTime.UtcNow;
            query.ExpireAt = DateTime.UtcNow.AddDays(7);
            await _queries.InsertOneAsync(query);
            return query;
        }

        public async Task<RentalQuery?> GetQueryByIdAsync(string queryId)
        {
            return await _queries.Find(q => q.Id == queryId).FirstOrDefaultAsync();
        }

        public async Task<List<RentalQuery>> GetQueriesForOwnerAsync(string ownerId)
        {
            return await _queries
                .Find(q => q.OwnerId == ownerId)
                .SortByDescending(q => q.LastUpdatedAt)
                .ToListAsync();
        }

        public async Task<List<RentalQuery>> GetQueriesForUserAsync(string userId)
        {
            return await _queries
                .Find(q => q.UserId == userId)
                .SortByDescending(q => q.LastUpdatedAt)
                .ToListAsync();
        }

        public async Task<RentalQuery?> AddReplyAsync(string queryId, QueryMessage message, bool isOwnerReply)
        {
            var update = Builders<RentalQuery>.Update
                .Push(q => q.Messages, message)
                .Set(q => q.LastUpdatedAt, DateTime.UtcNow)
                .Set(q => q.ExpireAt, DateTime.UtcNow.AddDays(7)); // Reset TTL on new message

            if (isOwnerReply)
            {
                update = update
                    .Set(q => q.IsReadByUser, false)
                    .Inc(q => q.UnreadCountForUser, 1)
                    .Set(q => q.IsReadByOwner, true)
                    .Set(q => q.UnreadCountForOwner, 0);
            }
            else
            {
                update = update
                    .Set(q => q.IsReadByOwner, false)
                    .Inc(q => q.UnreadCountForOwner, 1)
                    .Set(q => q.IsReadByUser, true)
                    .Set(q => q.UnreadCountForUser, 0);
            }

            var options = new FindOneAndUpdateOptions<RentalQuery, RentalQuery>
            {
                ReturnDocument = ReturnDocument.After
            };

            return await _queries.FindOneAndUpdateAsync<RentalQuery>(
                q => q.Id == queryId,
                update,
                options
            );
        }

        public async Task MarkAsReadByOwnerAsync(string queryId)
        {
            var update = Builders<RentalQuery>.Update
                .Set(q => q.IsReadByOwner, true)
                .Set(q => q.UnreadCountForOwner, 0);

            await _queries.UpdateOneAsync(q => q.Id == queryId, update);
        }

        public async Task MarkAsReadByUserAsync(string queryId)
        {
            var update = Builders<RentalQuery>.Update
                .Set(q => q.IsReadByUser, true)
                .Set(q => q.UnreadCountForUser, 0);

            await _queries.UpdateOneAsync(q => q.Id == queryId, update);
        }

        public async Task<int> GetUnreadCountForOwnerAsync(string ownerId)
        {
            return (int)await _queries.CountDocumentsAsync(
                q => q.OwnerId == ownerId && q.UnreadCountForOwner > 0
            );
        }

        public async Task<int> GetUnreadCountForUserAsync(string userId)
        {
            return (int)await _queries.CountDocumentsAsync(
                q => q.UserId == userId && q.UnreadCountForUser > 0
            );
        }

        public async Task DeleteQueryAsync(string queryId)
        {
            await _queries.DeleteOneAsync(q => q.Id == queryId);
        }
    }
}
