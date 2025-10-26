using Microsoft.AspNetCore.Identity;
using ServConnect.Models;

namespace ServConnect.Services
{
    public interface IServiceTransferService
    {
        // Create transfer request
        Task<ServiceTransfer> CreateTransferRequestAsync(string bookingId, Guid originalProviderId, Guid newProviderId, string? transferReason);

        // Get transfer requests
        Task<List<ServiceTransfer>> GetTransferRequestsForUserAsync(Guid userId);
        Task<List<ServiceTransfer>> GetTransferRequestsForProviderAsync(Guid providerId);
        Task<List<ServiceTransfer>> GetPendingTransferRequestsForProviderAsync(Guid providerId);
        Task<ServiceTransfer?> GetTransferRequestByIdAsync(string transferId);
        Task<ServiceTransfer?> GetTransferRequestByBookingIdAsync(string bookingId);

        // User actions
        Task<bool> UserApproveTransferAsync(string transferId, Guid userId, string? userMessage);
        Task<bool> UserRejectTransferAsync(string transferId, Guid userId, string? userMessage);

        // Provider actions
        Task<bool> ProviderAcceptTransferAsync(string transferId, Guid newProviderId, string? providerMessage);
        Task<bool> ProviderRejectTransferAsync(string transferId, Guid newProviderId, string? providerMessage);

        // Cancel transfer
        Task<bool> CancelTransferRequestAsync(string transferId, Guid originalProviderId);

        // Complete transfer (update booking with new provider)
        Task<bool> CompleteTransferAsync(string transferId);

        // Get available providers for transfer
        Task<List<ProviderService>> GetAvailableProvidersForTransferAsync(string providerServiceId, Guid excludeProviderId);
    }
}
