using ServConnect.Models;

namespace ServConnect.Services
{
    public interface IAdvertisementService
    {
        Task<Advertisement> CreateAsync(Advertisement ad);
        Task<List<Advertisement>> GetAllAsync();
        Task<Advertisement?> GetLatestActiveAsync();
        Task<List<Advertisement>> GetActiveAsync(int take = 10);
        Task<Advertisement?> GetLatestActiveByTypeAsync(AdvertisementType type);
        Task<List<Advertisement>> GetActiveByTypeAsync(AdvertisementType type, int take = 10);
        Task<bool> DeleteAsync(string id);
    }
}