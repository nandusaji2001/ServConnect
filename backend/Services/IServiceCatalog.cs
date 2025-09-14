using ServConnect.Models;

namespace ServConnect.Services
{
    public interface IServiceCatalog
    {
        // Predefined list (configurable)
        IReadOnlyList<string> GetPredefined();

        // Custom service definitions management
        Task<ServiceDefinition?> GetBySlugAsync(string slug);
        Task<ServiceDefinition> EnsureCustomAsync(string name, Guid createdByProviderId);
        Task<List<ServiceDefinition>> GetAllCustomAsync();

        // Provider links
        Task<ProviderService> LinkProviderAsync(Guid providerId, string serviceName, string description = "", decimal price = 0, string priceUnit = "per service", string currency = "USD", List<string> availableDays = null, string availableHours = "9:00 AM - 6:00 PM");
        Task<List<ProviderService>> GetProviderLinksBySlugAsync(string slug);
        Task<List<ProviderService>> GetProviderLinksByProviderAsync(Guid providerId);
        Task<bool> UnlinkAsync(string linkId, Guid providerId);

        // Aggregation
        Task<List<string>> GetAllAvailableServiceNamesAsync();
    }
}