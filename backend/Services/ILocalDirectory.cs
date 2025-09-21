using ServConnect.Models;

namespace ServConnect.Services
{
    public interface ILocalDirectory
    {
        // Categories
        Task<IReadOnlyList<LocalCategory>> GetCategoriesAsync();
        Task<LocalCategory> EnsureCategoryAsync(string name);

        // Services
        Task<LocalService> CreateServiceAsync(LocalService svc);
        Task<bool> UpdateServiceAsync(LocalService svc);
        Task<bool> DeleteServiceAsync(string id);
        Task<LocalService?> GetServiceAsync(string id);
        Task<IReadOnlyList<LocalService>> GetByCategoryAsync(string categorySlug);
        Task<IReadOnlyList<LocalService>> SearchAsync(string? q, string? categorySlug, string? locationName);
    }
}