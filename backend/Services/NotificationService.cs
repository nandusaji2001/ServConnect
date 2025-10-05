using Microsoft.Extensions.Configuration;
using MongoDB.Driver;
using ServConnect.Models;

namespace ServConnect.Services
{
    public class NotificationService : INotificationService
    {
        private readonly IMongoCollection<Notification> _notifications;

        public NotificationService(IConfiguration config)
        {
            var conn = config["MongoDB:ConnectionString"] ?? "mongodb://localhost:27017";
            var dbName = config["MongoDB:DatabaseName"] ?? "ServConnectDb";
            var client = new MongoClient(conn);
            var db = client.GetDatabase(dbName);
            _notifications = db.GetCollection<Notification>("Notifications");
        }

        public async Task<List<Notification>> GetUserNotificationsAsync(string userId, int limit = 10)
        {
            return await _notifications
                .Find(n => n.UserId == userId)
                .SortByDescending(n => n.CreatedAt)
                .Limit(limit)
                .ToListAsync();
        }

        public async Task<int> GetUnreadCountAsync(string userId)
        {
            return (int)await _notifications
                .CountDocumentsAsync(n => n.UserId == userId && !n.IsRead);
        }

        public async Task<Notification> CreateNotificationAsync(string userId, string title, string message, NotificationType type, string? relatedEntityId = null, string? actionUrl = null)
        {
            var notification = new Notification
            {
                UserId = userId,
                Title = title,
                Message = message,
                Type = type,
                RelatedEntityId = relatedEntityId,
                ActionUrl = actionUrl,
                CreatedAt = DateTime.UtcNow
            };

            await _notifications.InsertOneAsync(notification);
            return notification;
        }

        public async Task MarkAsReadAsync(string notificationId)
        {
            var filter = Builders<Notification>.Filter.Eq(n => n.Id, notificationId);
            var update = Builders<Notification>.Update.Set(n => n.IsRead, true);
            await _notifications.UpdateOneAsync(filter, update);
        }

        public async Task MarkAllAsReadAsync(string userId)
        {
            var filter = Builders<Notification>.Filter.And(
                Builders<Notification>.Filter.Eq(n => n.UserId, userId),
                Builders<Notification>.Filter.Eq(n => n.IsRead, false)
            );
            var update = Builders<Notification>.Update.Set(n => n.IsRead, true);
            await _notifications.UpdateManyAsync(filter, update);
        }

        public async Task DeleteNotificationAsync(string notificationId)
        {
            var filter = Builders<Notification>.Filter.Eq(n => n.Id, notificationId);
            await _notifications.DeleteOneAsync(filter);
        }

        public async Task CreateBookingNotificationAsync(string providerId, Booking booking)
        {
            var title = "New Booking Request";
            var message = $"You have a new booking request for {booking.ServiceName} from {booking.UserName}";
            var actionUrl = "/provider/bookings";

            await CreateNotificationAsync(providerId, title, message, NotificationType.BookingReceived, booking.Id.ToString(), actionUrl);
        }

        public async Task CreatePaymentNotificationAsync(string providerId, decimal amount, string serviceName)
        {
            var title = "Payment Received";
            var message = $"You received ₹{amount:N0} payment for {serviceName}";

            await CreateNotificationAsync(providerId, title, message, NotificationType.PaymentReceived);
        }

        public async Task CreateReviewNotificationAsync(string providerId, string customerName, int rating, string serviceName)
        {
            var title = "New Review Received";
            var message = $"{customerName} gave you {rating} stars for {serviceName}";

            await CreateNotificationAsync(providerId, title, message, NotificationType.ReviewReceived);
        }

        public async Task CreateServiceExpiryNotificationAsync(string providerId, string serviceName, DateTime expiryDate)
        {
            var daysLeft = (expiryDate - DateTime.UtcNow).Days;
            var title = daysLeft <= 0 ? "Service Expired" : "Service Expiring Soon";
            var message = daysLeft <= 0 
                ? $"Your service '{serviceName}' has expired and is now inactive"
                : $"Your service '{serviceName}' will expire in {daysLeft} day(s)";

            var type = daysLeft <= 0 ? NotificationType.ServiceExpired : NotificationType.ServiceExpiring;
            var actionUrl = "/Services/Manage";

            await CreateNotificationAsync(providerId, title, message, type, null, actionUrl);
        }
    }
}
