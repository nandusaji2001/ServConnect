using ServConnect.Models;

namespace ServConnect.Services
{
    public interface IBookingService
    {
        Task<Booking> CreateAsync(Guid userId, string userName, string userEmail, Guid providerId, string providerName, string providerServiceId, string serviceName, DateTime serviceDateTime, string contactPhone, string address, string? note);
        Task<List<Booking>> GetForProviderAsync(Guid providerId);
        Task<List<Booking>> GetForUserAsync(Guid userId);
        Task<Booking?> GetByIdAsync(string id);
        Task<bool> SetStatusAsync(string id, Guid providerId, BookingStatus status, string? providerMessage);
        Task<bool> CompleteAsync(string id, Guid userId, int? rating, string? feedback);
    }
}