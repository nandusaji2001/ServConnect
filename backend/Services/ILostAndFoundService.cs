using ServConnect.Models;

namespace ServConnect.Services
{
    public interface ILostAndFoundService
    {
        // Found Item operations
        Task<LostFoundItem> CreateItemAsync(LostFoundItem item);
        Task<LostFoundItem?> GetItemByIdAsync(string id);
        Task<List<LostFoundItem>> GetAllItemsAsync(string? category = null, string? status = null);
        Task<List<LostFoundItem>> GetItemsByFoundUserAsync(Guid userId);
        Task<bool> UpdateItemStatusAsync(string id, string status);
        Task<bool> MarkAsReturnedAsync(string id, Guid verifiedClaimantId, string claimantName);
        Task<bool> AddItemImageAsync(string id, string imageUrl);

        // Claim operations
        Task<ItemClaim> CreateClaimAsync(ItemClaim claim);
        Task<ItemClaim?> GetClaimByIdAsync(string id);
        Task<List<ItemClaim>> GetClaimsForItemAsync(string itemId);
        Task<List<ItemClaim>> GetClaimsByUserAsync(Guid userId);
        Task<ItemClaim?> GetActiveClaimForUserAndItemAsync(Guid userId, string itemId);
        Task<bool> UpdateClaimStatusAsync(string claimId, string status, bool? isVerified = null, string? note = null);
        Task<bool> UpdateClaimDetailsAsync(string claimId, string newDetails, List<string>? newProofImages = null);
        Task<bool> AddClaimProofImageAsync(string claimId, string imageUrl);
        Task<bool> IsUserBlockedForItemAsync(Guid userId, string itemId);

        // Pending verification for found user
        Task<List<ItemClaim>> GetPendingClaimsForFoundUserAsync(Guid foundUserId);

        // Lost Item Report operations
        Task<LostItemReport> CreateLostReportAsync(LostItemReport report);
        Task<LostItemReport?> GetLostReportByIdAsync(string id);
        Task<List<LostItemReport>> GetAllLostReportsAsync(string? category = null, string? status = null);
        Task<List<LostItemReport>> GetLostReportsByUserAsync(Guid userId);
        Task<bool> MarkLostItemAsFoundAsync(string reportId, Guid foundByUserId, string foundByUserName, 
            string foundByUserEmail, string? foundByUserPhone, string? foundLocation, string? foundNote);
        Task<bool> MarkLostItemAsRecoveredAsync(string reportId);
        Task<bool> CloseLostReportAsync(string reportId);
        Task<bool> AddLostReportImageAsync(string id, string imageUrl);

        // Statistics
        Task<int> GetAvailableItemsCountAsync();
        Task<int> GetPendingClaimsCountAsync(Guid foundUserId);
        Task<int> GetActiveLostReportsCountAsync();
    }
}
