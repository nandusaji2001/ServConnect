using ServConnect.Models;

namespace ServConnect.ViewModels
{
    public class ServiceProvidersViewModel
    {
        public string ServiceName { get; set; } = string.Empty;
        public string ServiceSlug { get; set; } = string.Empty;
        public List<ProviderService> Providers { get; set; } = new();
    }
}