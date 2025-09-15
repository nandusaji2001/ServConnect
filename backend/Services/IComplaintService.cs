using ServConnect.Models;

namespace ServConnect.Services
{
    public interface IComplaintService
    {
        Task<Complaint> CreateAsync(Complaint complaint);
        Task<bool> UpdateStatusAsync(string id, string status, string? adminNote = null);
        Task<Complaint?> GetByIdAsync(string id);
        Task<List<Complaint>> GetAllAsync(string? status = null, string? role = null, string? category = null);
        Task<List<Complaint>> GetByComplainantAsync(Guid complainantId);
        Task<bool> AddEvidenceAsync(string id, string fileUrl);
    }
}