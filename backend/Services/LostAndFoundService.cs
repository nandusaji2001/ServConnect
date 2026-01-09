using MongoDB.Driver;
using ServConnect.Models;

namespace ServConnect.Services
{
    public class LostAndFoundService : ILostAndFoundService
    {
        private readonly IMongoCollection<LostFoundItem> _items;
        private readonly IMongoCollection<ItemClaim> _claims;

        public LostAndFoundService(IConfiguration config)
        {
            var conn = config["MongoDB:ConnectionString"] ?? "mongodb://localhost:27017";
            var dbName = config["MongoDB:DatabaseName"] ?? "ServConnectDb";
            var client = new MongoClient(conn);
            var db = client.GetDatabase(dbName);
            _items = db.GetCollection<LostFoundItem>("LostFoundItems");
            _claims = db.GetCollection<ItemClaim>("ItemClaims");
        }

        #region Item Operations

        public async Task<LostFoundItem> CreateItemAsync(LostFoundItem item)
        {
            item.CreatedAt = DateTime.UtcNow;
            item.UpdatedAt = item.CreatedAt;
            item.Status = LostFoundItemStatus.Available;
            await _items.InsertOneAsync(item);
            return item;
        }

        public async Task<LostFoundItem?> GetItemByIdAsync(string id)
        {
            return await _items.Find(i => i.Id == id).FirstOrDefaultAsync();
        }

        public async Task<List<LostFoundItem>> GetAllItemsAsync(string? category = null, string? status = null)
        {
            var filter = Builders<LostFoundItem>.Filter.Empty;

            if (!string.IsNullOrWhiteSpace(category))
                filter &= Builders<LostFoundItem>.Filter.Eq(i => i.Category, category);

            if (!string.IsNullOrWhiteSpace(status))
                filter &= Builders<LostFoundItem>.Filter.Eq(i => i.Status, status);

            return await _items.Find(filter)
                .SortByDescending(i => i.CreatedAt)
                .ToListAsync();
        }

        public async Task<List<LostFoundItem>> GetItemsByFoundUserAsync(Guid userId)
        {
            return await _items.Find(i => i.FoundByUserId == userId)
                .SortByDescending(i => i.CreatedAt)
                .ToListAsync();
        }

        public async Task<bool> UpdateItemStatusAsync(string id, string status)
        {
            var update = Builders<LostFoundItem>.Update
                .Set(i => i.Status, status)
                .Set(i => i.UpdatedAt, DateTime.UtcNow);

            var result = await _items.UpdateOneAsync(i => i.Id == id, update);
            return result.ModifiedCount == 1;
        }

        public async Task<bool> MarkAsReturnedAsync(string id, Guid verifiedClaimantId, string claimantName)
        {
            var update = Builders<LostFoundItem>.Update
                .Set(i => i.Status, LostFoundItemStatus.Returned)
                .Set(i => i.VerifiedClaimantId, verifiedClaimantId)
                .Set(i => i.VerifiedClaimantName, claimantName)
                .Set(i => i.ReturnedAt, DateTime.UtcNow)
                .Set(i => i.UpdatedAt, DateTime.UtcNow);

            var result = await _items.UpdateOneAsync(i => i.Id == id, update);
            return result.ModifiedCount == 1;
        }

        public async Task<bool> AddItemImageAsync(string id, string imageUrl)
        {
            var update = Builders<LostFoundItem>.Update
                .AddToSet(i => i.Images, imageUrl)
                .Set(i => i.UpdatedAt, DateTime.UtcNow);

            var result = await _items.UpdateOneAsync(i => i.Id == id, update);
            return result.ModifiedCount == 1;
        }

        #endregion

        #region Claim Operations

        public async Task<ItemClaim> CreateClaimAsync(ItemClaim claim)
        {
            claim.CreatedAt = DateTime.UtcNow;
            claim.UpdatedAt = claim.CreatedAt;
            claim.Status = ClaimStatus.Pending;
            claim.AttemptCount = 1;

            await _claims.InsertOneAsync(claim);

            // Update item status to ClaimPending
            await UpdateItemStatusAsync(claim.ItemId, LostFoundItemStatus.ClaimPending);

            return claim;
        }

        public async Task<ItemClaim?> GetClaimByIdAsync(string id)
        {
            var claim = await _claims.Find(c => c.Id == id).FirstOrDefaultAsync();
            if (claim != null)
            {
                claim.Item = await GetItemByIdAsync(claim.ItemId);
            }
            return claim;
        }

        public async Task<List<ItemClaim>> GetClaimsForItemAsync(string itemId)
        {
            return await _claims.Find(c => c.ItemId == itemId)
                .SortByDescending(c => c.CreatedAt)
                .ToListAsync();
        }

        public async Task<List<ItemClaim>> GetClaimsByUserAsync(Guid userId)
        {
            var claims = await _claims.Find(c => c.ClaimantId == userId)
                .SortByDescending(c => c.CreatedAt)
                .ToListAsync();

            // Load items for each claim
            foreach (var claim in claims)
            {
                claim.Item = await GetItemByIdAsync(claim.ItemId);
            }

            return claims;
        }

        public async Task<ItemClaim?> GetActiveClaimForUserAndItemAsync(Guid userId, string itemId)
        {
            return await _claims.Find(c => 
                c.ClaimantId == userId && 
                c.ItemId == itemId && 
                c.Status != ClaimStatus.Blocked)
                .FirstOrDefaultAsync();
        }

        public async Task<bool> UpdateClaimStatusAsync(string claimId, string status, bool? isVerified = null, string? note = null)
        {
            var updateDef = Builders<ItemClaim>.Update
                .Set(c => c.Status, status)
                .Set(c => c.UpdatedAt, DateTime.UtcNow);

            if (isVerified.HasValue)
            {
                updateDef = updateDef
                    .Set(c => c.IsVerifiedByFoundUser, isVerified.Value)
                    .Set(c => c.VerifiedAt, DateTime.UtcNow);
            }

            if (!string.IsNullOrEmpty(note))
            {
                updateDef = updateDef.Set(c => c.VerificationNote, note);
            }

            var result = await _claims.UpdateOneAsync(c => c.Id == claimId, updateDef);

            // If verified, update item status
            if (status == ClaimStatus.Verified)
            {
                var claim = await GetClaimByIdAsync(claimId);
                if (claim != null)
                {
                    await UpdateItemStatusAsync(claim.ItemId, LostFoundItemStatus.Verified);
                }
            }

            return result.ModifiedCount == 1;
        }

        public async Task<bool> UpdateClaimDetailsAsync(string claimId, string newDetails, List<string>? newProofImages = null)
        {
            var claim = await GetClaimByIdAsync(claimId);
            if (claim == null) return false;

            var updateDef = Builders<ItemClaim>.Update
                .Set(c => c.PrivateOwnershipDetails, newDetails)
                .Set(c => c.Status, ClaimStatus.Pending)
                .Set(c => c.AttemptCount, claim.AttemptCount + 1)
                .Set(c => c.IsVerifiedByFoundUser, null)
                .Set(c => c.VerificationNote, null)
                .Set(c => c.UpdatedAt, DateTime.UtcNow);

            if (newProofImages != null && newProofImages.Count > 0)
            {
                updateDef = updateDef.Set(c => c.ProofImages, newProofImages);
            }

            var result = await _claims.UpdateOneAsync(c => c.Id == claimId, updateDef);
            return result.ModifiedCount == 1;
        }

        public async Task<bool> AddClaimProofImageAsync(string claimId, string imageUrl)
        {
            var update = Builders<ItemClaim>.Update
                .AddToSet(c => c.ProofImages, imageUrl)
                .Set(c => c.UpdatedAt, DateTime.UtcNow);

            var result = await _claims.UpdateOneAsync(c => c.Id == claimId, update);
            return result.ModifiedCount == 1;
        }

        public async Task<bool> IsUserBlockedForItemAsync(Guid userId, string itemId)
        {
            var blockedClaim = await _claims.Find(c => 
                c.ClaimantId == userId && 
                c.ItemId == itemId && 
                c.Status == ClaimStatus.Blocked)
                .FirstOrDefaultAsync();

            return blockedClaim != null;
        }

        #endregion

        #region Found User Operations

        public async Task<List<ItemClaim>> GetPendingClaimsForFoundUserAsync(Guid foundUserId)
        {
            // Get all items found by this user
            var items = await GetItemsByFoundUserAsync(foundUserId);
            var itemIds = items.Select(i => i.Id).ToList();

            // Get pending claims for these items
            var claims = await _claims.Find(c => 
                itemIds.Contains(c.ItemId) && 
                c.Status == ClaimStatus.Pending)
                .SortByDescending(c => c.CreatedAt)
                .ToListAsync();

            // Load items for each claim
            foreach (var claim in claims)
            {
                claim.Item = items.FirstOrDefault(i => i.Id == claim.ItemId);
            }

            return claims;
        }

        #endregion

        #region Statistics

        public async Task<int> GetAvailableItemsCountAsync()
        {
            return (int)await _items.CountDocumentsAsync(i => i.Status == LostFoundItemStatus.Available);
        }

        public async Task<int> GetPendingClaimsCountAsync(Guid foundUserId)
        {
            var items = await GetItemsByFoundUserAsync(foundUserId);
            var itemIds = items.Select(i => i.Id).ToList();

            return (int)await _claims.CountDocumentsAsync(c => 
                itemIds.Contains(c.ItemId) && 
                c.Status == ClaimStatus.Pending);
        }

        #endregion
    }
}
