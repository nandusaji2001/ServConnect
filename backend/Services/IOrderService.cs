using ServConnect.Models;

namespace ServConnect.Services
{
    public interface IOrderService
    {
        Task<(Order order, string razorpayOrderId)> CreateOrderAsync(Guid userId, string userEmail, string itemId, int quantity, string shippingAddress);
        Task<(Order order, string razorpayOrderId)> CreateOrderWithAddressAsync(Guid userId, string userEmail, string itemId, int quantity, UserAddress address, string? userAddressId = null);
        Task<bool> VerifyPaymentAndConfirmAsync(string orderId, string razorpayOrderId, string razorpayPaymentId, string razorpaySignature);
        
        // New status management methods
        Task<bool> AcceptOrderAsync(string orderId, Guid vendorId);
        Task<bool> MarkPackedAsync(string orderId, Guid vendorId);
        Task<bool> MarkShippedAsync(string orderId, Guid vendorId, string? trackingUrl = null);
        Task<bool> MarkOutForDeliveryAsync(string orderId, Guid vendorId);
        Task<bool> MarkDeliveredAsync(string orderId, Guid userId);
        Task<bool> UpdateOrderStatusAsync(string orderId, Guid vendorId, OrderStatus status, string? trackingUrl = null);
        
        Task<bool> SetTrackingUrlAsync(string orderId, Guid vendorId, string trackingUrl);
        Task<bool> CancelAsync(string orderId, Guid actorId, bool isVendor);

        Task<List<Order>> GetOrdersForVendorAsync(Guid vendorId);
        Task<List<Order>> GetOrdersForUserAsync(Guid userId);
        Task<Order?> GetByIdAsync(string id);
    }
}