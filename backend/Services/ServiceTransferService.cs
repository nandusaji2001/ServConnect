using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Configuration;
using MongoDB.Driver;
using ServConnect.Models;

namespace ServConnect.Services
{
    public class ServiceTransferService : IServiceTransferService
    {
        private readonly IMongoCollection<ServiceTransfer> _transferCollection;
        private readonly IBookingService _bookingService;
        private readonly IServiceCatalog _serviceCatalog;
        private readonly INotificationService _notificationService;
        private readonly UserManager<Users> _userManager;

        public ServiceTransferService(IConfiguration config, IBookingService bookingService, 
            IServiceCatalog serviceCatalog, INotificationService notificationService, UserManager<Users> userManager)
        {
            var conn = config["MongoDB:ConnectionString"] ?? "mongodb://localhost:27017";
            var dbName = config["MongoDB:DatabaseName"] ?? "ServConnectDb";
            var client = new MongoClient(conn);
            var db = client.GetDatabase(dbName);
            _transferCollection = db.GetCollection<ServiceTransfer>("ServiceTransfers");
            _bookingService = bookingService;
            _serviceCatalog = serviceCatalog;
            _notificationService = notificationService;
            _userManager = userManager;
        }

        public async Task<ServiceTransfer> CreateTransferRequestAsync(string bookingId, Guid originalProviderId, Guid newProviderId, string? transferReason)
        {
            // Get booking details
            var booking = await _bookingService.GetByIdAsync(bookingId);
            if (booking == null)
                throw new ArgumentException("Booking not found");

            // Verify original provider owns the booking
            if (booking.ProviderId != originalProviderId)
                throw new UnauthorizedAccessException("Only the assigned provider can transfer this service");

            // Check if transfer request already exists for this booking
            var existingTransfer = await GetTransferRequestByBookingIdAsync(bookingId);
            if (existingTransfer != null && existingTransfer.Status == TransferStatus.PendingUserApproval)
                throw new InvalidOperationException("A transfer request is already pending for this booking");

            // Get provider details
            var originalProvider = await _userManager.FindByIdAsync(originalProviderId.ToString());
            var newProvider = await _userManager.FindByIdAsync(newProviderId.ToString());
            var user = await _userManager.FindByIdAsync(booking.UserId.ToString());

            if (originalProvider == null || newProvider == null || user == null)
                throw new ArgumentException("Invalid user or provider information");

            var transfer = new ServiceTransfer
            {
                BookingId = bookingId,
                OriginalProviderId = originalProviderId,
                OriginalProviderName = originalProvider.FullName,
                NewProviderId = newProviderId,
                NewProviderName = newProvider.FullName,
                UserId = booking.UserId,
                UserName = user.FullName,
                ServiceName = booking.ServiceName,
                ServiceDateTime = booking.ServiceDateTime,
                TransferReason = transferReason,
                Status = TransferStatus.PendingUserApproval
            };

            await _transferCollection.InsertOneAsync(transfer);

            // Send notification to user
            await _notificationService.CreateNotificationAsync(
                booking.UserId.ToString(),
                "Service Transfer Request",
                $"{originalProvider.FullName} wants to transfer your {booking.ServiceName} service to {newProvider.FullName}. Please review and approve or reject this request.",
                NotificationType.ServiceTransferRequest,
                transfer.Id,
                $"/Bookings/UserBookings"
            );

            return transfer;
        }

        public async Task<List<ServiceTransfer>> GetTransferRequestsForUserAsync(Guid userId)
        {
            return await _transferCollection
                .Find(t => t.UserId == userId)
                .SortByDescending(t => t.RequestedAtUtc)
                .ToListAsync();
        }

        public async Task<List<ServiceTransfer>> GetTransferRequestsForProviderAsync(Guid providerId)
        {
            return await _transferCollection
                .Find(t => t.OriginalProviderId == providerId || t.NewProviderId == providerId)
                .SortByDescending(t => t.RequestedAtUtc)
                .ToListAsync();
        }

        public async Task<List<ServiceTransfer>> GetPendingTransferRequestsForProviderAsync(Guid providerId)
        {
            return await _transferCollection
                .Find(t => t.NewProviderId == providerId && t.Status == TransferStatus.PendingProviderAcceptance)
                .SortByDescending(t => t.RequestedAtUtc)
                .ToListAsync();
        }

        public async Task<ServiceTransfer?> GetTransferRequestByIdAsync(string transferId)
        {
            return await _transferCollection
                .Find(t => t.Id == transferId)
                .FirstOrDefaultAsync();
        }

        public async Task<ServiceTransfer?> GetTransferRequestByBookingIdAsync(string bookingId)
        {
            return await _transferCollection
                .Find(t => t.BookingId == bookingId && 
                          (t.Status == TransferStatus.PendingUserApproval || 
                           t.Status == TransferStatus.PendingProviderAcceptance))
                .FirstOrDefaultAsync();
        }

        public async Task<bool> UserApproveTransferAsync(string transferId, Guid userId, string? userMessage)
        {
            var transfer = await GetTransferRequestByIdAsync(transferId);
            if (transfer == null || transfer.UserId != userId)
                return false;

            if (transfer.Status != TransferStatus.PendingUserApproval)
                return false;

            var update = Builders<ServiceTransfer>.Update
                .Set(t => t.Status, TransferStatus.PendingProviderAcceptance)
                .Set(t => t.UserRespondedAtUtc, DateTime.UtcNow)
                .Set(t => t.UserMessage, userMessage);

            var result = await _transferCollection.UpdateOneAsync(
                t => t.Id == transferId,
                update
            );

            if (result.ModifiedCount > 0)
            {
                // Send notification to new provider
                await _notificationService.CreateNotificationAsync(
                    transfer.NewProviderId.ToString(),
                    "Service Transfer Request",
                    $"{transfer.UserName} has approved the transfer of {transfer.ServiceName} service to you. Please accept or reject this request.",
                    NotificationType.ServiceTransferRequest,
                    transferId,
                    $"/ServiceProvider/Dashboard"
                );

                return true;
            }

            return false;
        }

        public async Task<bool> UserRejectTransferAsync(string transferId, Guid userId, string? userMessage)
        {
            var transfer = await GetTransferRequestByIdAsync(transferId);
            if (transfer == null || transfer.UserId != userId)
                return false;

            if (transfer.Status != TransferStatus.PendingUserApproval)
                return false;

            var update = Builders<ServiceTransfer>.Update
                .Set(t => t.Status, TransferStatus.RejectedByUser)
                .Set(t => t.UserRespondedAtUtc, DateTime.UtcNow)
                .Set(t => t.UserMessage, userMessage);

            var result = await _transferCollection.UpdateOneAsync(
                t => t.Id == transferId,
                update
            );

            if (result.ModifiedCount > 0)
            {
                // Send notification to original provider
                await _notificationService.CreateNotificationAsync(
                    transfer.OriginalProviderId.ToString(),
                    "Service Transfer Rejected",
                    $"{transfer.UserName} has rejected the transfer request for {transfer.ServiceName} service.",
                    NotificationType.ServiceTransferRejected,
                    transferId,
                    $"/ServiceProvider/ProviderBookings"
                );

                return true;
            }

            return false;
        }

        public async Task<bool> ProviderAcceptTransferAsync(string transferId, Guid newProviderId, string? providerMessage)
        {
            var transfer = await GetTransferRequestByIdAsync(transferId);
            if (transfer == null || transfer.NewProviderId != newProviderId)
                return false;

            if (transfer.Status != TransferStatus.PendingProviderAcceptance)
                return false;

            var update = Builders<ServiceTransfer>.Update
                .Set(t => t.Status, TransferStatus.Approved)
                .Set(t => t.ProviderRespondedAtUtc, DateTime.UtcNow)
                .Set(t => t.ProviderMessage, providerMessage);

            var result = await _transferCollection.UpdateOneAsync(
                t => t.Id == transferId,
                update
            );

            if (result.ModifiedCount > 0)
            {
                // Complete the transfer by updating the booking
                await CompleteTransferAsync(transferId);
                return true;
            }

            return false;
        }

        public async Task<bool> ProviderRejectTransferAsync(string transferId, Guid newProviderId, string? providerMessage)
        {
            var transfer = await GetTransferRequestByIdAsync(transferId);
            if (transfer == null || transfer.NewProviderId != newProviderId)
                return false;

            if (transfer.Status != TransferStatus.PendingProviderAcceptance)
                return false;

            var update = Builders<ServiceTransfer>.Update
                .Set(t => t.Status, TransferStatus.RejectedByProvider)
                .Set(t => t.ProviderRespondedAtUtc, DateTime.UtcNow)
                .Set(t => t.ProviderMessage, providerMessage);

            var result = await _transferCollection.UpdateOneAsync(
                t => t.Id == transferId,
                update
            );

            if (result.ModifiedCount > 0)
            {
                // Send notification to original provider and user
                await _notificationService.CreateNotificationAsync(
                    transfer.OriginalProviderId.ToString(),
                    "Service Transfer Rejected",
                    $"{transfer.NewProviderName} has rejected the transfer request for {transfer.ServiceName} service.",
                    NotificationType.ServiceTransferRejected,
                    transferId,
                    $"/ServiceProvider/ProviderBookings"
                );

                await _notificationService.CreateNotificationAsync(
                    transfer.UserId.ToString(),
                    "Service Transfer Rejected",
                    $"{transfer.NewProviderName} has rejected the transfer request. The service remains with {transfer.OriginalProviderName}.",
                    NotificationType.ServiceTransferRejected,
                    transferId,
                    $"/Bookings/UserBookings"
                );

                return true;
            }

            return false;
        }

        public async Task<bool> CancelTransferRequestAsync(string transferId, Guid originalProviderId)
        {
            var transfer = await GetTransferRequestByIdAsync(transferId);
            if (transfer == null || transfer.OriginalProviderId != originalProviderId)
                return false;

            if (transfer.Status != TransferStatus.PendingUserApproval && 
                transfer.Status != TransferStatus.PendingProviderAcceptance)
                return false;

            var update = Builders<ServiceTransfer>.Update
                .Set(t => t.Status, TransferStatus.Cancelled);

            var result = await _transferCollection.UpdateOneAsync(
                t => t.Id == transferId,
                update
            );

            return result.ModifiedCount > 0;
        }

        public async Task<bool> CompleteTransferAsync(string transferId)
        {
            var transfer = await GetTransferRequestByIdAsync(transferId);
            if (transfer == null || transfer.Status != TransferStatus.Approved)
                return false;

            // Update the booking with new provider information
            var booking = await _bookingService.GetByIdAsync(transfer.BookingId);
            if (booking == null)
                return false;

            // Update booking provider information
            var bookingUpdated = await _bookingService.UpdateProviderAsync(
                transfer.BookingId, 
                transfer.NewProviderId, 
                transfer.NewProviderName
            );

            if (!bookingUpdated)
                return false;

            // Mark the transfer as completed
            var update = Builders<ServiceTransfer>.Update
                .Set(t => t.TransferCompletedAtUtc, DateTime.UtcNow);

            var result = await _transferCollection.UpdateOneAsync(
                t => t.Id == transferId,
                update
            );

            if (result.ModifiedCount > 0)
            {
                // Send notifications to all parties
                await _notificationService.CreateNotificationAsync(
                    transfer.UserId.ToString(),
                    "Service Transfer Completed",
                    $"Your {transfer.ServiceName} service has been successfully transferred to {transfer.NewProviderName}.",
                    NotificationType.ServiceTransferCompleted,
                    transferId,
                    $"/Bookings/UserBookings"
                );

                await _notificationService.CreateNotificationAsync(
                    transfer.OriginalProviderId.ToString(),
                    "Service Transfer Completed",
                    $"The {transfer.ServiceName} service has been successfully transferred to {transfer.NewProviderName}.",
                    NotificationType.ServiceTransferCompleted,
                    transferId,
                    $"/ServiceProvider/ProviderBookings"
                );

                await _notificationService.CreateNotificationAsync(
                    transfer.NewProviderId.ToString(),
                    "Service Transfer Completed",
                    $"You have successfully received the {transfer.ServiceName} service from {transfer.OriginalProviderName}.",
                    NotificationType.ServiceTransferCompleted,
                    transferId,
                    $"/ServiceProvider/ProviderBookings"
                );

                return true;
            }

            return false;
        }

        public async Task<List<ProviderService>> GetAvailableProvidersForTransferAsync(string providerServiceId, Guid excludeProviderId)
        {
            try
            {
                Console.WriteLine($"[DEBUG] Getting available providers for service ID: {providerServiceId}, excluding provider: {excludeProviderId}");
                
                // Get the original service to find similar services
                var originalService = await _serviceCatalog.GetProviderServiceByIdAsync(providerServiceId);
                if (originalService == null)
                {
                    Console.WriteLine($"[DEBUG] Original service not found for ID: {providerServiceId}");
                    return new List<ProviderService>();
                }

                Console.WriteLine($"[DEBUG] Found original service: {originalService.ServiceName}");

                // Find other providers offering the same service
                var availableServices = await _serviceCatalog.GetActiveServicesByNameAsync(originalService.ServiceName);
                
                Console.WriteLine($"[DEBUG] Found {availableServices.Count} services with name: {originalService.ServiceName}");
                
                // Debug: Show all services before filtering
                foreach (var service in availableServices)
                {
                    Console.WriteLine($"[DEBUG] Service: {service.ProviderName} - ProviderId: {service.ProviderId}, IsActive: {service.IsActive}, EndDate: {service.PublicationEndDate}");
                }
                
                // First, just exclude the original provider (less restrictive filtering)
                var excludedProviderServices = availableServices
                    .Where(s => s.ProviderId != excludeProviderId)
                    .ToList();
                
                Console.WriteLine($"[DEBUG] After excluding original provider: {excludedProviderServices.Count} services");
                
                // Then filter by active status
                var activeServices = excludedProviderServices
                    .Where(s => s.IsActive)
                    .ToList();
                
                Console.WriteLine($"[DEBUG] After filtering by IsActive: {activeServices.Count} services");
                
                // Finally filter by expiry (if needed)
                var filteredServices = activeServices
                    .Where(s => !s.PublicationEndDate.HasValue || s.PublicationEndDate.Value >= DateTime.UtcNow)
                    .ToList();
                
                Console.WriteLine($"[DEBUG] After filtering by expiry: {filteredServices.Count} services");
                
                Console.WriteLine($"[DEBUG] After filtering: {filteredServices.Count} available providers");
                Console.WriteLine($"[DEBUG] Excluded provider ID: {excludeProviderId}");
                
                // Debug: Show filtered services
                foreach (var service in filteredServices)
                {
                    Console.WriteLine($"[DEBUG] Available provider: {service.ProviderName} - ProviderId: {service.ProviderId}");
                }
                
                // For debugging: if no providers found, return all available services to see what's there
                if (filteredServices.Count == 0)
                {
                    Console.WriteLine($"[DEBUG] No providers found after filtering. Returning all available services for debugging:");
                    foreach (var service in availableServices)
                    {
                        Console.WriteLine($"[DEBUG] All services: {service.ProviderName} - ProviderId: {service.ProviderId}, IsActive: {service.IsActive}");
                    }
                    // Temporarily return all services for debugging (remove this later)
                    return availableServices.Take(5).ToList(); // Limit to 5 for testing
                }
                
                return filteredServices;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ERROR] GetAvailableProvidersForTransferAsync failed: {ex.Message}");
                Console.WriteLine($"[ERROR] Stack trace: {ex.StackTrace}");
                return new List<ProviderService>();
            }
        }
    }
}
