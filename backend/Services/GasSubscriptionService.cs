using MongoDB.Driver;
using Microsoft.AspNetCore.Identity;
using ServConnect.Models;

namespace ServConnect.Services
{
    public class GasSubscriptionService : IGasSubscriptionService
    {
        private readonly IMongoCollection<GasSubscription> _subscriptions;
        private readonly IMongoCollection<GasReading> _readings;
        private readonly IMongoCollection<GasOrder> _orders;
        private readonly IMongoCollection<Item> _items;
        private readonly UserManager<Users> _userManager;
        private readonly INotificationService _notificationService;
        private readonly ILogger<GasSubscriptionService> _logger;

        // Weight thresholds for 2kg cylinder (in grams)
        private const double FULL_WEIGHT = 2000.0;    // 2kg = Full
        private const double HALF_WEIGHT = 1000.0;    // 1kg = Half
        private const double LOW_WEIGHT = 500.0;      // 500g = Low (trigger threshold)
        private const double CRITICAL_WEIGHT = 200.0; // 200g = Critical

        public GasSubscriptionService(
            IConfiguration config,
            UserManager<Users> userManager,
            INotificationService notificationService,
            ILogger<GasSubscriptionService> logger)
        {
            var conn = config["MongoDB:ConnectionString"] ?? "mongodb://localhost:27017";
            var dbName = config["MongoDB:DatabaseName"] ?? "ServConnectDb";
            var client = new MongoClient(conn);
            var db = client.GetDatabase(dbName);

            _subscriptions = db.GetCollection<GasSubscription>("GasSubscriptions");
            _readings = db.GetCollection<GasReading>("GasReadings");
            _orders = db.GetCollection<GasOrder>("GasOrders");
            _items = db.GetCollection<Item>("Items");
            _userManager = userManager;
            _notificationService = notificationService;
            _logger = logger;

            // Create indexes for efficient queries
            CreateIndexes();
        }

        private void CreateIndexes()
        {
            // Subscription indexes
            _subscriptions.Indexes.CreateOne(new CreateIndexModel<GasSubscription>(
                Builders<GasSubscription>.IndexKeys.Ascending(s => s.UserId),
                new CreateIndexOptions { Unique = true }));

            _subscriptions.Indexes.CreateOne(new CreateIndexModel<GasSubscription>(
                Builders<GasSubscription>.IndexKeys.Ascending(s => s.DeviceId)));

            // Reading indexes
            _readings.Indexes.CreateOne(new CreateIndexModel<GasReading>(
                Builders<GasReading>.IndexKeys
                    .Ascending(r => r.UserId)
                    .Descending(r => r.Timestamp)));

            _readings.Indexes.CreateOne(new CreateIndexModel<GasReading>(
                Builders<GasReading>.IndexKeys.Ascending(r => r.DeviceId)));

            // Order indexes
            _orders.Indexes.CreateOne(new CreateIndexModel<GasOrder>(
                Builders<GasOrder>.IndexKeys
                    .Ascending(o => o.UserId)
                    .Descending(o => o.CreatedAt)));

            _orders.Indexes.CreateOne(new CreateIndexModel<GasOrder>(
                Builders<GasOrder>.IndexKeys
                    .Ascending(o => o.VendorId)
                    .Descending(o => o.CreatedAt)));
        }

        #region Subscription Management

        public async Task<GasSubscription?> GetSubscriptionByUserIdAsync(Guid userId)
        {
            return await _subscriptions
                .Find(s => s.UserId == userId)
                .FirstOrDefaultAsync();
        }

        public async Task<GasSubscription?> GetSubscriptionByDeviceIdAsync(string deviceId)
        {
            return await _subscriptions
                .Find(s => s.DeviceId == deviceId)
                .FirstOrDefaultAsync();
        }

        public async Task<GasSubscription> CreateOrUpdateSubscriptionAsync(Guid userId, GasSubscriptionRequest request)
        {
            var user = await _userManager.FindByIdAsync(userId.ToString());
            if (user == null)
                throw new InvalidOperationException("User not found");

            var existing = await GetSubscriptionByUserIdAsync(userId);

            Users? vendor = null;
            if (!string.IsNullOrEmpty(request.PreferredVendorId) && Guid.TryParse(request.PreferredVendorId, out var vendorGuid))
            {
                vendor = await _userManager.FindByIdAsync(request.PreferredVendorId);
            }

            if (existing != null)
            {
                // Update existing subscription
                var update = Builders<GasSubscription>.Update
                    .Set(s => s.IsAutoBookingEnabled, request.IsAutoBookingEnabled)
                    .Set(s => s.PreferredVendorId, vendor != null ? Guid.Parse(request.PreferredVendorId!) : (Guid?)null)
                    .Set(s => s.PreferredVendorName, vendor?.BusinessName ?? vendor?.FullName)
                    .Set(s => s.ThresholdPercentage, request.ThresholdPercentage)
                    .Set(s => s.DeviceId, request.DeviceId)
                    .Set(s => s.DeliveryAddress, request.DeliveryAddress)
                    .Set(s => s.UserPhone, request.UserPhone)
                    .Set(s => s.FullCylinderWeightGrams, request.FullCylinderWeightGrams)
                    .Set(s => s.TareCylinderWeightGrams, request.TareCylinderWeightGrams)
                    .Set(s => s.UpdatedAt, DateTime.UtcNow);

                await _subscriptions.UpdateOneAsync(s => s.Id == existing.Id, update);

                existing = await GetSubscriptionByUserIdAsync(userId);
                return existing!;
            }
            else
            {
                // Create new subscription
                var subscription = new GasSubscription
                {
                    UserId = userId,
                    UserName = user.FullName,
                    UserEmail = user.Email ?? string.Empty,
                    UserPhone = request.UserPhone,
                    DeliveryAddress = request.DeliveryAddress,
                    IsAutoBookingEnabled = request.IsAutoBookingEnabled,
                    PreferredVendorId = vendor != null ? Guid.Parse(request.PreferredVendorId!) : null,
                    PreferredVendorName = vendor?.BusinessName ?? vendor?.FullName,
                    ThresholdPercentage = request.ThresholdPercentage,
                    DeviceId = request.DeviceId,
                    FullCylinderWeightGrams = request.FullCylinderWeightGrams,
                    TareCylinderWeightGrams = request.TareCylinderWeightGrams,
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                };

                await _subscriptions.InsertOneAsync(subscription);
                return subscription;
            }
        }

        public async Task<bool> DeleteSubscriptionAsync(Guid userId)
        {
            var result = await _subscriptions.DeleteOneAsync(s => s.UserId == userId);
            return result.DeletedCount > 0;
        }

        #endregion

        #region Gas Readings

        public async Task<GasReading> ProcessGasReadingAsync(GasReadingRequest request)
        {
            var deviceId = request.DeviceId ?? "ESP32-DEFAULT";
            var weightKg = request.Weight;
            var weightGrams = weightKg * 1000.0;

            _logger.LogInformation("Processing gas reading: Device={DeviceId}, Weight={Weight}kg ({WeightGrams}g)",
                deviceId, weightKg, weightGrams);

            // Find subscription by device ID
            var subscription = await GetSubscriptionByDeviceIdAsync(deviceId);

            if (subscription == null)
            {
                _logger.LogWarning("No subscription found for device {DeviceId}", deviceId);
                // Still record the reading but without user association
                var orphanReading = new GasReading
                {
                    UserId = Guid.Empty,
                    DeviceId = deviceId,
                    WeightGrams = weightGrams,
                    GasPercentage = 0,
                    Status = "Unregistered Device",
                    Timestamp = DateTime.UtcNow,
                    BatteryLevel = request.BatteryLevel
                };
                await _readings.InsertOneAsync(orphanReading);
                return orphanReading;
            }

            // Calculate gas percentage based on cylinder weights
            var netGasWeight = weightGrams - subscription.TareCylinderWeightGrams;
            var maxGasWeight = subscription.FullCylinderWeightGrams - subscription.TareCylinderWeightGrams;
            var gasPercentage = Math.Max(0, Math.Min(100, (netGasWeight / maxGasWeight) * 100));

            // Determine status
            var status = GetGasStatus(weightGrams, subscription.FullCylinderWeightGrams);

            // Create reading record
            var reading = new GasReading
            {
                UserId = subscription.UserId,
                DeviceId = deviceId,
                WeightGrams = weightGrams,
                GasPercentage = Math.Round(gasPercentage, 1),
                Status = status,
                Timestamp = DateTime.UtcNow,
                BatteryLevel = request.BatteryLevel
            };

            await _readings.InsertOneAsync(reading);

            // Update subscription with latest reading
            var subUpdate = Builders<GasSubscription>.Update
                .Set(s => s.LastRecordedWeightGrams, weightGrams)
                .Set(s => s.LastGasPercentage, reading.GasPercentage)
                .Set(s => s.LastReadingAt, reading.Timestamp);

            await _subscriptions.UpdateOneAsync(s => s.Id == subscription.Id, subUpdate);

            // Check if auto-booking should be triggered
            await CheckAndTriggerAutoBookingAsync(subscription, reading);

            // Cleanup old readings (keep only last 500)
            await CleanupOldReadingsAsync(subscription.UserId);

            return reading;
        }

        private string GetGasStatus(double weightGrams, double fullWeight)
        {
            var percentage = (weightGrams / fullWeight) * 100;

            if (percentage >= 80) return "Full";
            if (percentage >= 50) return "Good";
            if (percentage >= 25) return "Half";
            if (percentage >= 10) return "Low";
            return "Critical";
        }

        private async Task CheckAndTriggerAutoBookingAsync(GasSubscription subscription, GasReading reading)
        {
            // Skip if auto-booking is disabled
            if (!subscription.IsAutoBookingEnabled)
            {
                _logger.LogDebug("Auto-booking disabled for user {UserId}", subscription.UserId);
                return;
            }

            // Skip if no preferred vendor set
            if (subscription.PreferredVendorId == null)
            {
                _logger.LogDebug("No preferred vendor set for user {UserId}", subscription.UserId);
                return;
            }

            // Skip if booking is already pending
            if (subscription.IsBookingPending)
            {
                _logger.LogDebug("Booking already pending for user {UserId}", subscription.UserId);
                return;
            }

            // Check if gas level is below threshold
            if (reading.GasPercentage < subscription.ThresholdPercentage)
            {
                _logger.LogInformation("Gas level {Percentage}% below threshold {Threshold}% for user {UserId}. Triggering auto-booking.",
                    reading.GasPercentage, subscription.ThresholdPercentage, subscription.UserId);

                try
                {
                    // Create automatic gas order
                    var order = await CreateGasOrderAsync(
                        subscription.UserId,
                        subscription.PreferredVendorId.Value,
                        isAutoTriggered: true,
                        triggerPercentage: reading.GasPercentage);

                    // Mark subscription as having pending booking
                    var update = Builders<GasSubscription>.Update
                        .Set(s => s.IsBookingPending, true)
                        .Set(s => s.CurrentPendingOrderId, order.Id);

                    await _subscriptions.UpdateOneAsync(s => s.Id == subscription.Id, update);

                    // Send notification to user
                    await _notificationService.CreateNotificationAsync(
                        subscription.UserId.ToString(),
                        "🔥 Low Gas Alert - Order Placed!",
                        $"Your gas level is at {reading.GasPercentage:F1}%. An automatic order has been placed with {subscription.PreferredVendorName}.",
                        NotificationType.GasAutoBookingTriggered,
                        order.Id,
                        $"/GasSubscription/OrderDetails/{order.Id}");

                    // Send notification to vendor
                    await _notificationService.CreateNotificationAsync(
                        subscription.PreferredVendorId.Value.ToString(),
                        "📦 New Gas Order (Auto-Triggered)",
                        $"New automatic gas order from {subscription.UserName}. Gas level: {reading.GasPercentage:F1}%",
                        NotificationType.GasAutoBookingTriggered,
                        order.Id,
                        $"/Vendor/GasOrders");

                    _logger.LogInformation("Auto-booking triggered successfully. Order ID: {OrderId}", order.Id);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to trigger auto-booking for user {UserId}", subscription.UserId);
                }
            }
        }

        private async Task CleanupOldReadingsAsync(Guid userId)
        {
            // Keep only the last 500 readings per user
            var allReadings = await _readings
                .Find(r => r.UserId == userId)
                .SortByDescending(r => r.Timestamp)
                .Project(r => r.Id)
                .ToListAsync();

            if (allReadings.Count > 500)
            {
                var idsToDelete = allReadings.Skip(500).ToList();
                await _readings.DeleteManyAsync(r => idsToDelete.Contains(r.Id));
            }
        }

        public async Task<List<GasReading>> GetRecentReadingsAsync(Guid userId, int count = 50)
        {
            return await _readings
                .Find(r => r.UserId == userId)
                .SortByDescending(r => r.Timestamp)
                .Limit(count)
                .ToListAsync();
        }

        public async Task<GasReading?> GetLatestReadingAsync(Guid userId)
        {
            return await _readings
                .Find(r => r.UserId == userId)
                .SortByDescending(r => r.Timestamp)
                .FirstOrDefaultAsync();
        }

        #endregion

        #region Gas Orders

        public async Task<GasOrder> CreateGasOrderAsync(Guid userId, Guid vendorId, bool isAutoTriggered, double? triggerPercentage = null)
        {
            var user = await _userManager.FindByIdAsync(userId.ToString());
            var vendor = await _userManager.FindByIdAsync(vendorId.ToString());

            if (user == null)
                throw new InvalidOperationException("User not found");
            if (vendor == null)
                throw new InvalidOperationException("Vendor not found");

            var subscription = await GetSubscriptionByUserIdAsync(userId);

            // Try to find a gas item from the vendor
            var gasItem = await _items
                .Find(i => i.OwnerId == vendorId &&
                           i.IsActive &&
                           (i.Category == "Gas" || i.Category == "LPG" ||
                            i.Title.ToLower().Contains("gas") || i.Title.ToLower().Contains("lpg")))
                .FirstOrDefaultAsync();

            var order = new GasOrder
            {
                UserId = userId,
                UserName = user.FullName,
                UserEmail = user.Email ?? string.Empty,
                UserPhone = subscription?.UserPhone ?? user.PhoneNumber ?? string.Empty,
                DeliveryAddress = subscription?.DeliveryAddress ?? user.Address ?? string.Empty,
                VendorId = vendorId,
                VendorName = vendor.BusinessName ?? vendor.FullName,
                IsAutoTriggered = isAutoTriggered,
                TriggerGasPercentage = triggerPercentage,
                GasItemId = gasItem?.Id,
                GasItemName = gasItem?.Title ?? "LPG Gas Cylinder (2kg)",
                Price = gasItem?.Price ?? 500.00m, // Default price if no item found
                Status = GasOrderStatus.Pending,
                CreatedAt = DateTime.UtcNow,
                PreDeliveryWeightGrams = subscription?.LastRecordedWeightGrams
            };

            await _orders.InsertOneAsync(order);
            return order;
        }

        public async Task<GasOrder?> GetOrderByIdAsync(string orderId)
        {
            return await _orders
                .Find(o => o.Id == orderId)
                .FirstOrDefaultAsync();
        }

        public async Task<List<GasOrder>> GetUserOrdersAsync(Guid userId, int limit = 20)
        {
            return await _orders
                .Find(o => o.UserId == userId)
                .SortByDescending(o => o.CreatedAt)
                .Limit(limit)
                .ToListAsync();
        }

        public async Task<List<GasOrder>> GetVendorOrdersAsync(Guid vendorId, int limit = 50)
        {
            return await _orders
                .Find(o => o.VendorId == vendorId)
                .SortByDescending(o => o.CreatedAt)
                .Limit(limit)
                .ToListAsync();
        }

        public async Task<List<GasOrder>> GetPendingVendorOrdersAsync(Guid vendorId)
        {
            return await _orders
                .Find(o => o.VendorId == vendorId &&
                          (o.Status == GasOrderStatus.Pending ||
                           o.Status == GasOrderStatus.Accepted ||
                           o.Status == GasOrderStatus.OutForDelivery))
                .SortByDescending(o => o.CreatedAt)
                .ToListAsync();
        }

        public async Task<GasOrder?> UpdateOrderStatusAsync(string orderId, GasOrderStatus status, string? vendorMessage = null)
        {
            var order = await GetOrderByIdAsync(orderId);
            if (order == null) return null;

            var updateBuilder = Builders<GasOrder>.Update
                .Set(o => o.Status, status)
                .Set(o => o.UpdatedAt, DateTime.UtcNow);

            if (!string.IsNullOrEmpty(vendorMessage))
                updateBuilder = updateBuilder.Set(o => o.VendorMessage, vendorMessage);

            switch (status)
            {
                case GasOrderStatus.Accepted:
                    updateBuilder = updateBuilder.Set(o => o.AcceptedAt, DateTime.UtcNow);
                    await _notificationService.CreateNotificationAsync(
                        order.UserId.ToString(),
                        "✅ Gas Order Accepted",
                        $"Your gas order has been accepted by {order.VendorName}.",
                        NotificationType.GasOrderAccepted,
                        orderId);
                    break;

                case GasOrderStatus.OutForDelivery:
                    updateBuilder = updateBuilder.Set(o => o.OutForDeliveryAt, DateTime.UtcNow);
                    await _notificationService.CreateNotificationAsync(
                        order.UserId.ToString(),
                        "🚚 Gas Cylinder Out for Delivery",
                        $"Your gas cylinder is on its way! Expect delivery soon.",
                        NotificationType.GasOrderOutForDelivery,
                        orderId);
                    break;

                case GasOrderStatus.Delivered:
                    updateBuilder = updateBuilder.Set(o => o.DeliveredAt, DateTime.UtcNow);
                    // Reset the pending flag on subscription
                    await ResetSubscriptionPendingFlagAsync(order.UserId);
                    await _notificationService.CreateNotificationAsync(
                        order.UserId.ToString(),
                        "🎉 Gas Cylinder Delivered",
                        $"Your gas cylinder has been delivered. Enjoy uninterrupted cooking!",
                        NotificationType.GasOrderDelivered,
                        orderId);
                    break;

                case GasOrderStatus.Cancelled:
                case GasOrderStatus.Rejected:
                    await ResetSubscriptionPendingFlagAsync(order.UserId);
                    await _notificationService.CreateNotificationAsync(
                        order.UserId.ToString(),
                        status == GasOrderStatus.Cancelled ? "❌ Gas Order Cancelled" : "❌ Gas Order Rejected",
                        vendorMessage ?? "Your gas order has been " + status.ToString().ToLower(),
                        NotificationType.GasOrderCancelled,
                        orderId);
                    break;
            }

            await _orders.UpdateOneAsync(o => o.Id == orderId, updateBuilder);
            return await GetOrderByIdAsync(orderId);
        }

        private async Task ResetSubscriptionPendingFlagAsync(Guid userId)
        {
            var update = Builders<GasSubscription>.Update
                .Set(s => s.IsBookingPending, false)
                .Set(s => s.CurrentPendingOrderId, null);

            await _subscriptions.UpdateOneAsync(s => s.UserId == userId, update);
        }

        public async Task<bool> VerifyDeliveryByWeightAsync(string orderId, double newWeightGrams)
        {
            var order = await GetOrderByIdAsync(orderId);
            if (order == null) return false;

            var subscription = await GetSubscriptionByUserIdAsync(order.UserId);
            if (subscription == null) return false;

            // Check if weight increased significantly (indicating new cylinder)
            var weightIncrease = newWeightGrams - (order.PreDeliveryWeightGrams ?? 0);
            var expectedIncrease = subscription.FullCylinderWeightGrams * 0.5; // Expect at least 50% increase

            var isVerified = weightIncrease >= expectedIncrease;

            var update = Builders<GasOrder>.Update
                .Set(o => o.PostDeliveryWeightGrams, newWeightGrams)
                .Set(o => o.IsDeliveryVerified, isVerified);

            await _orders.UpdateOneAsync(o => o.Id == orderId, update);

            if (isVerified)
            {
                _logger.LogInformation("Delivery verified for order {OrderId}. Weight increased by {Increase}g",
                    orderId, weightIncrease);

                // Auto-complete the order if verified
                await UpdateOrderStatusAsync(orderId, GasOrderStatus.Delivered, "Delivery verified automatically by weight sensor");
            }

            return isVerified;
        }

        #endregion

        #region Vendor Management

        public async Task<List<Users>> GetGasVendorsAsync()
        {
            // Get all vendors (users with Vendor role)
            var vendors = await _userManager.GetUsersInRoleAsync(RoleTypes.Vendor);
            return vendors.Where(v => v.IsAdminApproved).ToList();
        }

        #endregion

        #region Dashboard

        public async Task<GasSubscriptionDashboard> GetUserDashboardAsync(Guid userId)
        {
            var subscription = await GetSubscriptionByUserIdAsync(userId);
            var latestReading = await GetLatestReadingAsync(userId);
            // No longer fetching readings for chart - removed for performance
            var orders = await GetUserOrdersAsync(userId, 5);

            GasOrder? currentOrder = null;
            if (subscription?.CurrentPendingOrderId != null)
            {
                currentOrder = await GetOrderByIdAsync(subscription.CurrentPendingOrderId);
            }

            return new GasSubscriptionDashboard
            {
                Subscription = subscription,
                CurrentGasPercentage = latestReading?.GasPercentage ?? 0,
                GasStatus = latestReading?.Status ?? "No Data",
                CurrentWeightGrams = latestReading?.WeightGrams ?? 0,
                RecentReadings = new List<GasReading>(), // Chart removed for performance
                CurrentOrder = currentOrder,
                OrderHistory = orders
            };
        }

        #endregion
    }
}
