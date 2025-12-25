using ServConnect.Models;

namespace ServConnect.Services
{
    public interface IComplaintService
    {
        Task<Complaint> CreateAsync(Complaint complaint);
        Task<Complaint?> GetByIdAsync(string id);
        Task<List<Complaint>> GetAllAsync(string? status = null, string? category = null, string? priority = null, bool? priorityOnly = null);
        Task<List<Complaint>> GetByComplainantAsync(Guid complainantId);
        Task<bool> UpdateStatusAsync(string id, string status, string? adminNote = null, string? changedBy = null);
        Task<bool> RejectAsync(string id, string rejectionReason, string? changedBy = null);
        Task<bool> ResolveAsync(string id, string resolution, string? changedBy = null);
        Task<bool> AssignAsync(string id, string assignedTo, string assignedToName);
        Task<bool> EscalateAsync(string id, string priority, string? note = null, string? changedBy = null);
        Task<bool> SuspendTargetAsync(string id, string reason, string? changedBy = null);
        Task<bool> UnsuspendTargetAsync(string id, string? changedBy = null);
        Task<bool> AddEvidenceAsync(string id, string fileUrl);
        Task<int> GetPendingCountAsync();
        Task<int> GetPriorityCountAsync();
    }
}
