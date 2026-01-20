using ServConnect.Models;

namespace ServConnect.Services
{
    public interface IGasSubscriptionService
    {
        // Subscription Management
        Task<GasSubscription?> GetSubscriptionByUserIdAsync(Guid userId);
        Task<GasSubscription?> GetSubscriptionByDeviceIdAsync(string deviceId);
        Task<GasSubscription> CreateOrUpdateSubscriptionAsync(Guid userId, GasSubscriptionRequest request);
        Task<bool> DeleteSubscriptionAsync(Guid userId);

        // Gas Readings
        Task<GasReading> ProcessGasReadingAsync(GasReadingRequest request);
        Task<List<GasReading>> GetRecentReadingsAsync(Guid userId, int count = 50);
        Task<GasReading?> GetLatestReadingAsync(Guid userId);

        // Gas Orders
        Task<GasOrder> CreateGasOrderAsync(Guid userId, Guid vendorId, bool isAutoTriggered, double? triggerPercentage = null);
        Task<GasOrder?> GetOrderByIdAsync(string orderId);
        Task<List<GasOrder>> GetUserOrdersAsync(Guid userId, int limit = 20);
        Task<List<GasOrder>> GetVendorOrdersAsync(Guid vendorId, int limit = 50);
        Task<List<GasOrder>> GetPendingVendorOrdersAsync(Guid vendorId);
        Task<GasOrder?> UpdateOrderStatusAsync(string orderId, GasOrderStatus status, string? vendorMessage = null);
        Task<bool> VerifyDeliveryByWeightAsync(string orderId, double newWeightGrams);

        // Vendor Management
        Task<List<Users>> GetGasVendorsAsync();

        // Dashboard
        Task<GasSubscriptionDashboard> GetUserDashboardAsync(Guid userId);
    }
}
