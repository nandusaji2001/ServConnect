using ServConnect.Models;

namespace ServConnect.Services
{
    public interface IAdvertisementRequestService
    {
        Task<AdvertisementRequest> CreateAsync(AdvertisementRequest req);
        Task<bool> MarkPaidAsync(string id, string razorpayOrderId, string razorpayPaymentId, string razorpaySignature);
        Task<List<AdvertisementRequest>> GetAllAsync(AdRequestStatus? status = null);
        Task<List<AdvertisementRequest>> GetByUserAsync(Guid userId);
        Task<AdvertisementRequest?> GetByIdAsync(string id);
        Task<bool> UpdateStatusAsync(string id, AdRequestStatus status, string? adminNote = null);
    }
}