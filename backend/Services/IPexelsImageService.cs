namespace ServConnect.Services
{
    public interface IPexelsImageService
    {
        Task<ServiceImageResult> GetImageForServiceAsync(string serviceName);
    }

    public class ServiceImageResult
    {
        public string? ImageUrl { get; set; }
        public string? PhotographerName { get; set; }
        public string? PhotographerUrl { get; set; }
    }
}
