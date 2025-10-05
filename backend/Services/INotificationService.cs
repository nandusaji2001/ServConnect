using ServConnect.Models;

namespace ServConnect.Services
{
    public interface INotificationService
    {
        Task<List<Notification>> GetUserNotificationsAsync(string userId, int limit = 10);
        Task<int> GetUnreadCountAsync(string userId);
        Task<Notification> CreateNotificationAsync(string userId, string title, string message, NotificationType type, string? relatedEntityId = null, string? actionUrl = null);
        Task MarkAsReadAsync(string notificationId);
        Task MarkAllAsReadAsync(string userId);
        Task DeleteNotificationAsync(string notificationId);
        
        // Specific notification creators
        Task CreateBookingNotificationAsync(string providerId, Booking booking);
        Task CreatePaymentNotificationAsync(string providerId, decimal amount, string serviceName);
        Task CreateReviewNotificationAsync(string providerId, string customerName, int rating, string serviceName);
        Task CreateServiceExpiryNotificationAsync(string providerId, string serviceName, DateTime expiryDate);
    }
}
