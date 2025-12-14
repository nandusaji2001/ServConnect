using Microsoft.Extensions.Configuration;
using MongoDB.Driver;
using ServConnect.Models;

namespace ServConnect.Services
{
    public interface IRentalSubscriptionService
    {
        Task<RentalSubscription> CreateSubscriptionAsync(string userId, RentalSubscriptionPlan plan);
        Task<RentalSubscription?> GetActiveSubscriptionAsync(string userId);
        Task<RentalSubscriptionStatusDto> GetSubscriptionStatusAsync(string userId);
        Task<bool> HasActiveSubscriptionAsync(string userId);
        Task<RentalSubscription?> GetByIdAsync(string id);
        Task<RentalSubscription?> GetByOrderIdAsync(string orderId);
        Task<bool> UpdatePaymentStatusAsync(string subscriptionId, RentalPaymentStatus status, string? paymentId = null);
        Task<List<RentalSubscription>> GetUserSubscriptionHistoryAsync(string userId);
        Task<bool> ActivateSubscriptionAsync(string subscriptionId, string paymentId);
    }

    public class RentalSubscriptionService : IRentalSubscriptionService
    {
        private readonly IMongoCollection<RentalSubscription> _subscriptions;
        private readonly ILogger<RentalSubscriptionService> _logger;

        public RentalSubscriptionService(IConfiguration configuration, ILogger<RentalSubscriptionService> logger)
        {
            var connectionString = configuration["MongoDB:ConnectionString"] ?? "mongodb://localhost:27017";
            var dbName = configuration["MongoDB:DatabaseName"] ?? "ServConnectDb";
            var client = new MongoClient(connectionString);
            var database = client.GetDatabase(dbName);
            _subscriptions = database.GetCollection<RentalSubscription>("RentalSubscriptions");
            _logger = logger;

            // Create indexes
            CreateIndexes();
        }

        private void CreateIndexes()
        {
            try
            {
                var indexKeys = Builders<RentalSubscription>.IndexKeys
                    .Ascending(s => s.UserId)
                    .Descending(s => s.ExpiryDate);
                _subscriptions.Indexes.CreateOne(new CreateIndexModel<RentalSubscription>(indexKeys));

                var orderIdIndex = Builders<RentalSubscription>.IndexKeys.Ascending(s => s.RazorpayOrderId);
                _subscriptions.Indexes.CreateOne(new CreateIndexModel<RentalSubscription>(orderIdIndex));
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to create indexes for RentalSubscriptions");
            }
        }

        public async Task<RentalSubscription> CreateSubscriptionAsync(string userId, RentalSubscriptionPlan plan)
        {
            var price = RentalSubscriptionPricing.GetPrice(plan);
            var durationDays = RentalSubscriptionPricing.GetDurationDays(plan);

            // Check if user has an active subscription
            var activeSubscription = await GetActiveSubscriptionAsync(userId);
            DateTime startDate;
            
            if (activeSubscription != null && activeSubscription.ExpiryDate > DateTime.UtcNow)
            {
                // Extend from current expiry date
                startDate = activeSubscription.ExpiryDate;
            }
            else
            {
                startDate = DateTime.UtcNow;
            }

            var subscription = new RentalSubscription
            {
                UserId = userId,
                Plan = plan,
                AmountPaid = price,
                PaymentStatus = RentalPaymentStatus.Pending,
                StartDate = startDate,
                ExpiryDate = startDate.AddDays(durationDays),
                CreatedAt = DateTime.UtcNow
            };

            await _subscriptions.InsertOneAsync(subscription);
            return subscription;
        }

        public async Task<RentalSubscription?> GetActiveSubscriptionAsync(string userId)
        {
            return await _subscriptions
                .Find(s => s.UserId == userId && 
                           s.PaymentStatus == RentalPaymentStatus.Completed && 
                           s.ExpiryDate > DateTime.UtcNow)
                .SortByDescending(s => s.ExpiryDate)
                .FirstOrDefaultAsync();
        }

        public async Task<RentalSubscriptionStatusDto> GetSubscriptionStatusAsync(string userId)
        {
            var activeSubscription = await GetActiveSubscriptionAsync(userId);

            if (activeSubscription == null || !activeSubscription.IsActive)
            {
                return new RentalSubscriptionStatusDto
                {
                    HasActiveSubscription = false,
                    CurrentPlan = null,
                    ExpiryDate = null,
                    DaysRemaining = null
                };
            }

            var daysRemaining = (int)(activeSubscription.ExpiryDate - DateTime.UtcNow).TotalDays;

            return new RentalSubscriptionStatusDto
            {
                HasActiveSubscription = true,
                CurrentPlan = activeSubscription.Plan,
                ExpiryDate = activeSubscription.ExpiryDate,
                DaysRemaining = Math.Max(0, daysRemaining)
            };
        }

        public async Task<bool> HasActiveSubscriptionAsync(string userId)
        {
            var count = await _subscriptions.CountDocumentsAsync(
                s => s.UserId == userId && 
                     s.PaymentStatus == RentalPaymentStatus.Completed && 
                     s.ExpiryDate > DateTime.UtcNow);
            return count > 0;
        }

        public async Task<RentalSubscription?> GetByIdAsync(string id)
        {
            return await _subscriptions.Find(s => s.Id == id).FirstOrDefaultAsync();
        }

        public async Task<RentalSubscription?> GetByOrderIdAsync(string orderId)
        {
            return await _subscriptions.Find(s => s.RazorpayOrderId == orderId).FirstOrDefaultAsync();
        }

        public async Task<bool> UpdatePaymentStatusAsync(string subscriptionId, RentalPaymentStatus status, string? paymentId = null)
        {
            var updateBuilder = Builders<RentalSubscription>.Update
                .Set(s => s.PaymentStatus, status);

            if (!string.IsNullOrEmpty(paymentId))
            {
                updateBuilder = updateBuilder.Set(s => s.RazorpayPaymentId, paymentId);
            }

            var result = await _subscriptions.UpdateOneAsync(
                s => s.Id == subscriptionId,
                updateBuilder);

            return result.ModifiedCount > 0;
        }

        public async Task<bool> ActivateSubscriptionAsync(string subscriptionId, string paymentId)
        {
            var subscription = await GetByIdAsync(subscriptionId);
            if (subscription == null) return false;

            // If the subscription was created with a future start date (extending existing),
            // we keep it as is. Otherwise, update start date to now.
            var update = Builders<RentalSubscription>.Update
                .Set(s => s.PaymentStatus, RentalPaymentStatus.Completed)
                .Set(s => s.RazorpayPaymentId, paymentId);

            // If this is a new subscription (not extending), update dates
            if (subscription.PaymentStatus == RentalPaymentStatus.Pending && subscription.StartDate <= DateTime.UtcNow)
            {
                var durationDays = RentalSubscriptionPricing.GetDurationDays(subscription.Plan);
                update = update
                    .Set(s => s.StartDate, DateTime.UtcNow)
                    .Set(s => s.ExpiryDate, DateTime.UtcNow.AddDays(durationDays));
            }

            var result = await _subscriptions.UpdateOneAsync(
                s => s.Id == subscriptionId,
                update);

            return result.ModifiedCount > 0;
        }

        public async Task<List<RentalSubscription>> GetUserSubscriptionHistoryAsync(string userId)
        {
            return await _subscriptions
                .Find(s => s.UserId == userId)
                .SortByDescending(s => s.CreatedAt)
                .ToListAsync();
        }
    }
}
