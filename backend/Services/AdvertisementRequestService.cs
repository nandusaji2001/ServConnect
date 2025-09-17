using Microsoft.Extensions.Configuration;
using MongoDB.Driver;
using ServConnect.Models;

namespace ServConnect.Services
{
    public class AdvertisementRequestService : IAdvertisementRequestService
    {
        private readonly IMongoCollection<AdvertisementRequest> _requests;

        public AdvertisementRequestService(IConfiguration config)
        {
            var conn = config["MongoDB:ConnectionString"] ?? "mongodb://localhost:27017";
            var dbName = config["MongoDB:DatabaseName"] ?? "ServConnectDb";
            var client = new MongoClient(conn);
            var db = client.GetDatabase(dbName);
            _requests = db.GetCollection<AdvertisementRequest>("AdvertisementRequests");
        }

        public async Task<AdvertisementRequest> CreateAsync(AdvertisementRequest req)
        {
            await _requests.InsertOneAsync(req);
            return req;
        }

        public async Task<bool> MarkPaidAsync(string id, string razorpayOrderId, string razorpayPaymentId, string razorpaySignature)
        {
            var update = Builders<AdvertisementRequest>.Update
                .Set(r => r.RazorpayOrderId, razorpayOrderId)
                .Set(r => r.RazorpayPaymentId, razorpayPaymentId)
                .Set(r => r.RazorpaySignature, razorpaySignature)
                .Set(r => r.IsPaid, true);

            var res = await _requests.UpdateOneAsync(r => r.Id == id, update);
            return res.ModifiedCount == 1;
        }

        public async Task<List<AdvertisementRequest>> GetAllAsync(AdRequestStatus? status = null)
        {
            if (status.HasValue)
            {
                return await _requests.Find(r => r.Status == status.Value)
                                      .SortByDescending(r => r.CreatedAt)
                                      .ToListAsync();
            }
            return await _requests.Find(Builders<AdvertisementRequest>.Filter.Empty)
                                  .SortByDescending(r => r.CreatedAt)
                                  .ToListAsync();
        }

        public async Task<List<AdvertisementRequest>> GetByUserAsync(Guid userId)
        {
            return await _requests.Find(r => r.RequestedByUserId == userId)
                                  .SortByDescending(r => r.CreatedAt)
                                  .ToListAsync();
        }

        public async Task<AdvertisementRequest?> GetByIdAsync(string id)
        {
            return await _requests.Find(r => r.Id == id).FirstOrDefaultAsync();
        }

        public async Task<bool> UpdateStatusAsync(string id, AdRequestStatus status, string? adminNote = null)
        {
            var update = Builders<AdvertisementRequest>.Update
                .Set(r => r.Status, status)
                .Set(r => r.ReviewedAtUtc, DateTime.UtcNow)
                .Set(r => r.AdminNote, adminNote);
            var res = await _requests.UpdateOneAsync(r => r.Id == id, update);
            return res.ModifiedCount == 1;
        }
    }
}