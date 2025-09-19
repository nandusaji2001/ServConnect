using ServConnect.Models;

namespace ServConnect.Services
{
    public interface IItemService
    {
        Task<Item> CreateAsync(Item item);
        Task<Item?> GetByIdAsync(string id);
        Task<List<Item>> GetByOwnerAsync(Guid ownerId);
        Task<List<Item>> GetAllAsync(bool includeInactive = false);
        Task<bool> UpdateAsync(Item item);
        Task<bool> DeleteAsync(string id);

        // Inventory operations
        Task<bool> ReduceStockAsync(string itemId, int quantity);
        Task<bool> IncreaseStockAsync(string itemId, int quantity);
    }
}