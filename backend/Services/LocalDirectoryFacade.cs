using ServConnect.Models;
using System.Linq;

namespace ServConnect.Services
{
    // Facade that keeps Mongo-backed admin CRUD and uses OSM/Nominatim for public discovery
    public class LocalDirectoryFacade : ILocalDirectory
    {
        private readonly LocalDirectory _mongo;
        private readonly NominatimSearchService _nominatim;

        public LocalDirectoryFacade(LocalDirectory mongo, NominatimSearchService nominatim)
        {
            _mongo = mongo;
            _nominatim = nominatim;
        }

        // Admin / shared operations delegate to Mongo implementation
        public Task<IReadOnlyList<LocalCategory>> GetCategoriesAsync() => _mongo.GetCategoriesAsync();
        public Task<LocalCategory> EnsureCategoryAsync(string name) => _mongo.EnsureCategoryAsync(name);
        public Task<LocalService> CreateServiceAsync(LocalService svc) => _mongo.CreateServiceAsync(svc);
        public Task<bool> UpdateServiceAsync(LocalService svc) => _mongo.UpdateServiceAsync(svc);
        public Task<bool> DeleteServiceAsync(string id) => _mongo.DeleteServiceAsync(id);
        public Task<LocalService?> GetServiceAsync(string id) => _mongo.GetServiceAsync(id);
        public Task<IReadOnlyList<LocalService>> GetByCategoryAsync(string categorySlug) => _mongo.GetByCategoryAsync(categorySlug);

        // Public discovery uses OSM
        public async Task<IReadOnlyList<LocalService>> SearchAsync(string? q, string? categorySlug, string? locationName)
        {
            // Resolve category name from slug for better queries
            string? categoryName = null;
            if (!string.IsNullOrWhiteSpace(categorySlug))
            {
                var cats = await _mongo.GetCategoriesAsync();
                categoryName = cats.FirstOrDefault(c => c.Slug == categorySlug)?.Name;
            }
            var location = string.IsNullOrWhiteSpace(locationName) ? "Kattappana" : locationName!;
            return await _nominatim.SearchAsync(q, categoryName, location);
        }
    }
}