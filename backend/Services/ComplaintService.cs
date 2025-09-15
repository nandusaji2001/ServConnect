using Microsoft.Extensions.Configuration;
using MongoDB.Driver;
using ServConnect.Models;

namespace ServConnect.Services
{
    public class ComplaintService : IComplaintService
    {
        private readonly IMongoCollection<Complaint> _complaints;

        public ComplaintService(IConfiguration config)
        {
            var conn = config["MongoDB:ConnectionString"] ?? "mongodb://localhost:27017";
            var dbName = config["MongoDB:DatabaseName"] ?? "ServConnectDb";
            var client = new MongoClient(conn);
            var db = client.GetDatabase(dbName);
            _complaints = db.GetCollection<Complaint>("Complaints");
        }

        public async Task<Complaint> CreateAsync(Complaint complaint)
        {
            complaint.CreatedAt = DateTime.UtcNow;
            complaint.UpdatedAt = complaint.CreatedAt;
            await _complaints.InsertOneAsync(complaint);
            return complaint;
        }

        public async Task<bool> UpdateStatusAsync(string id, string status, string? adminNote = null)
        {
            var update = Builders<Complaint>.Update
                .Set(c => c.Status, status)
                .Set(c => c.UpdatedAt, DateTime.UtcNow)
                .Set(c => c.AdminNote, adminNote);
            var res = await _complaints.UpdateOneAsync(c => c.Id == id, update);
            return res.ModifiedCount == 1;
        }

        public async Task<Complaint?> GetByIdAsync(string id)
        {
            return await _complaints.Find(c => c.Id == id).FirstOrDefaultAsync();
        }

        public async Task<List<Complaint>> GetAllAsync(string? status = null, string? role = null, string? category = null)
        {
            var filter = Builders<Complaint>.Filter.Empty;
            if (!string.IsNullOrWhiteSpace(status))
                filter &= Builders<Complaint>.Filter.Eq(c => c.Status, status);
            if (!string.IsNullOrWhiteSpace(role))
                filter &= Builders<Complaint>.Filter.Eq(c => c.ComplainantRole, role);
            if (!string.IsNullOrWhiteSpace(category))
                filter &= Builders<Complaint>.Filter.Eq(c => c.Category, category);

            return await _complaints.Find(filter)
                .SortByDescending(c => c.CreatedAt)
                .ToListAsync();
        }

        public async Task<List<Complaint>> GetByComplainantAsync(Guid complainantId)
        {
            return await _complaints
                .Find(c => c.ComplainantId == complainantId)
                .SortByDescending(c => c.CreatedAt)
                .ToListAsync();
        }

        public async Task<bool> AddEvidenceAsync(string id, string fileUrl)
        {
            var update = Builders<Complaint>.Update
                .AddToSet(c => c.EvidenceFiles, fileUrl)
                .Set(c => c.UpdatedAt, DateTime.UtcNow);
            var res = await _complaints.UpdateOneAsync(c => c.Id == id, update);
            return res.ModifiedCount == 1;
        }
    }
}