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
            complaint.Status = ComplaintStatus.Pending;
            
            // Auto-set priority for elderly and safety concerns
            if (complaint.IsElderly || complaint.Category == ComplaintCategory.SafetyEmergency)
            {
                complaint.Priority = complaint.Category == ComplaintCategory.SafetyEmergency 
                    ? ComplaintPriority.Critical 
                    : ComplaintPriority.High;
            }

            complaint.StatusHistory.Add(new ComplaintStatusHistory
            {
                Status = ComplaintStatus.Pending,
                Note = "Complaint submitted",
                ChangedAt = complaint.CreatedAt
            });

            await _complaints.InsertOneAsync(complaint);
            return complaint;
        }

        public async Task<Complaint?> GetByIdAsync(string id)
        {
            return await _complaints.Find(c => c.Id == id).FirstOrDefaultAsync();
        }

        public async Task<List<Complaint>> GetAllAsync(string? status = null, string? category = null, string? priority = null, bool? priorityOnly = null)
        {
            var filter = Builders<Complaint>.Filter.Empty;
            
            if (!string.IsNullOrWhiteSpace(status))
                filter &= Builders<Complaint>.Filter.Eq(c => c.Status, status);
            
            if (!string.IsNullOrWhiteSpace(category))
                filter &= Builders<Complaint>.Filter.Eq(c => c.Category, category);
            
            if (!string.IsNullOrWhiteSpace(priority))
                filter &= Builders<Complaint>.Filter.Eq(c => c.Priority, priority);
            
            if (priorityOnly == true)
            {
                filter &= Builders<Complaint>.Filter.Or(
                    Builders<Complaint>.Filter.Eq(c => c.Priority, ComplaintPriority.High),
                    Builders<Complaint>.Filter.Eq(c => c.Priority, ComplaintPriority.Critical),
                    Builders<Complaint>.Filter.Eq(c => c.IsElderly, true),
                    Builders<Complaint>.Filter.Eq(c => c.Category, ComplaintCategory.SafetyEmergency)
                );
            }

            // Sort by priority (Critical first, then High, then Normal) and then by CreatedAt descending
            var sort = Builders<Complaint>.Sort
                .Descending(c => c.Priority)
                .Descending(c => c.CreatedAt);

            return await _complaints.Find(filter)
                .Sort(sort)
                .ToListAsync();
        }

        public async Task<List<Complaint>> GetByComplainantAsync(Guid complainantId)
        {
            return await _complaints
                .Find(c => c.ComplainantId == complainantId)
                .SortByDescending(c => c.CreatedAt)
                .ToListAsync();
        }

        public async Task<bool> UpdateStatusAsync(string id, string status, string? adminNote = null, string? changedBy = null)
        {
            var update = Builders<Complaint>.Update
                .Set(c => c.Status, status)
                .Set(c => c.UpdatedAt, DateTime.UtcNow)
                .Set(c => c.AdminNote, adminNote)
                .Push(c => c.StatusHistory, new ComplaintStatusHistory
                {
                    Status = status,
                    Note = adminNote,
                    ChangedBy = changedBy,
                    ChangedAt = DateTime.UtcNow
                });

            var res = await _complaints.UpdateOneAsync(c => c.Id == id, update);
            return res.ModifiedCount == 1;
        }

        public async Task<bool> RejectAsync(string id, string rejectionReason, string? changedBy = null)
        {
            var update = Builders<Complaint>.Update
                .Set(c => c.Status, ComplaintStatus.Rejected)
                .Set(c => c.RejectionReason, rejectionReason)
                .Set(c => c.UpdatedAt, DateTime.UtcNow)
                .Push(c => c.StatusHistory, new ComplaintStatusHistory
                {
                    Status = ComplaintStatus.Rejected,
                    Note = rejectionReason,
                    ChangedBy = changedBy,
                    ChangedAt = DateTime.UtcNow
                });

            var res = await _complaints.UpdateOneAsync(c => c.Id == id, update);
            return res.ModifiedCount == 1;
        }

        public async Task<bool> ResolveAsync(string id, string resolution, string? changedBy = null)
        {
            var update = Builders<Complaint>.Update
                .Set(c => c.Status, ComplaintStatus.Resolved)
                .Set(c => c.Resolution, resolution)
                .Set(c => c.ResolvedAt, DateTime.UtcNow)
                .Set(c => c.UpdatedAt, DateTime.UtcNow)
                .Push(c => c.StatusHistory, new ComplaintStatusHistory
                {
                    Status = ComplaintStatus.Resolved,
                    Note = resolution,
                    ChangedBy = changedBy,
                    ChangedAt = DateTime.UtcNow
                });

            var res = await _complaints.UpdateOneAsync(c => c.Id == id, update);
            return res.ModifiedCount == 1;
        }

        public async Task<bool> AssignAsync(string id, string assignedTo, string assignedToName)
        {
            var update = Builders<Complaint>.Update
                .Set(c => c.AssignedTo, assignedTo)
                .Set(c => c.AssignedToName, assignedToName)
                .Set(c => c.UpdatedAt, DateTime.UtcNow);

            var res = await _complaints.UpdateOneAsync(c => c.Id == id, update);
            return res.ModifiedCount == 1;
        }

        public async Task<bool> EscalateAsync(string id, string priority, string? note = null, string? changedBy = null)
        {
            var update = Builders<Complaint>.Update
                .Set(c => c.Priority, priority)
                .Set(c => c.UpdatedAt, DateTime.UtcNow)
                .Push(c => c.StatusHistory, new ComplaintStatusHistory
                {
                    Status = $"Escalated to {priority}",
                    Note = note,
                    ChangedBy = changedBy,
                    ChangedAt = DateTime.UtcNow
                });

            var res = await _complaints.UpdateOneAsync(c => c.Id == id, update);
            return res.ModifiedCount == 1;
        }

        public async Task<bool> SuspendTargetAsync(string id, string reason, string? changedBy = null)
        {
            var update = Builders<Complaint>.Update
                .Set(c => c.TargetSuspended, true)
                .Set(c => c.SuspendedAt, DateTime.UtcNow)
                .Set(c => c.SuspensionReason, reason)
                .Set(c => c.UpdatedAt, DateTime.UtcNow)
                .Push(c => c.StatusHistory, new ComplaintStatusHistory
                {
                    Status = "Target Suspended",
                    Note = reason,
                    ChangedBy = changedBy,
                    ChangedAt = DateTime.UtcNow
                });

            var res = await _complaints.UpdateOneAsync(c => c.Id == id, update);
            return res.ModifiedCount == 1;
        }

        public async Task<bool> UnsuspendTargetAsync(string id, string? changedBy = null)
        {
            var update = Builders<Complaint>.Update
                .Set(c => c.TargetSuspended, false)
                .Set(c => c.SuspendedAt, (DateTime?)null)
                .Set(c => c.SuspensionReason, (string?)null)
                .Set(c => c.UpdatedAt, DateTime.UtcNow)
                .Push(c => c.StatusHistory, new ComplaintStatusHistory
                {
                    Status = "Suspension Lifted",
                    ChangedBy = changedBy,
                    ChangedAt = DateTime.UtcNow
                });

            var res = await _complaints.UpdateOneAsync(c => c.Id == id, update);
            return res.ModifiedCount == 1;
        }

        public async Task<bool> AddEvidenceAsync(string id, string fileUrl)
        {
            var update = Builders<Complaint>.Update
                .AddToSet(c => c.EvidenceFiles, fileUrl)
                .Set(c => c.UpdatedAt, DateTime.UtcNow);
            var res = await _complaints.UpdateOneAsync(c => c.Id == id, update);
            return res.ModifiedCount == 1;
        }

        public async Task<int> GetPendingCountAsync()
        {
            return (int)await _complaints.CountDocumentsAsync(c => c.Status == ComplaintStatus.Pending);
        }

        public async Task<int> GetPriorityCountAsync()
        {
            var filter = Builders<Complaint>.Filter.And(
                Builders<Complaint>.Filter.Ne(c => c.Status, ComplaintStatus.Resolved),
                Builders<Complaint>.Filter.Ne(c => c.Status, ComplaintStatus.Rejected),
                Builders<Complaint>.Filter.Or(
                    Builders<Complaint>.Filter.Eq(c => c.Priority, ComplaintPriority.High),
                    Builders<Complaint>.Filter.Eq(c => c.Priority, ComplaintPriority.Critical)
                )
            );
            return (int)await _complaints.CountDocumentsAsync(filter);
        }
    }
}
