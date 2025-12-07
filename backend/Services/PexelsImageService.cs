using System.Text.Json;
using Microsoft.Extensions.Configuration;

namespace ServConnect.Services
{
    public class PexelsImageService : IPexelsImageService
    {
        private readonly HttpClient _httpClient;
        private readonly IConfiguration _configuration;
        private readonly ILogger<PexelsImageService> _logger;

        public PexelsImageService(HttpClient httpClient, IConfiguration configuration, ILogger<PexelsImageService> logger)
        {
            _httpClient = httpClient;
            _configuration = configuration;
            _logger = logger;
        }

        public async Task<ServiceImageResult> GetImageForServiceAsync(string serviceName)
        {
            try
            {
                var apiKey = _configuration["PexelsApi:ApiKey"];
                var baseUrl = _configuration["PexelsApi:BaseUrl"];

                if (string.IsNullOrEmpty(apiKey) || apiKey == "YOUR_PEXELS_API_KEY_HERE")
                {
                    _logger.LogWarning("Pexels API key not configured");
                    return new ServiceImageResult();
                }

                var query = serviceName.ToLower().Trim();
                var url = $"{baseUrl}/search?query={Uri.EscapeDataString(query)}&per_page=1&page=1";

                using (var request = new HttpRequestMessage(HttpMethod.Get, url))
                {
                    request.Headers.Add("Authorization", apiKey);

                    var response = await _httpClient.SendAsync(request);

                    if (!response.IsSuccessStatusCode)
                    {
                        _logger.LogWarning($"Pexels API returned status {response.StatusCode}");
                        return new ServiceImageResult();
                    }

                    var content = await response.Content.ReadAsStringAsync();
                    using (var doc = JsonDocument.Parse(content))
                    {
                        var root = doc.RootElement;

                        if (root.TryGetProperty("photos", out var photosElement) && photosElement.GetArrayLength() > 0)
                        {
                            var photo = photosElement[0];

                            var imageUrl = photo.TryGetProperty("src", out var src) &&
                                          src.TryGetProperty("large", out var large)
                                ? large.GetString()
                                : null;

                            var photographerName = photo.TryGetProperty("photographer", out var photographer)
                                ? photographer.GetString()
                                : "Pexels";

                            var photographerUrl = photo.TryGetProperty("photographer_url", out var photogUrl)
                                ? photogUrl.GetString()
                                : "https://www.pexels.com";

                            return new ServiceImageResult
                            {
                                ImageUrl = imageUrl,
                                PhotographerName = photographerName,
                                PhotographerUrl = photographerUrl
                            };
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error fetching image from Pexels: {ex.Message}");
            }

            return new ServiceImageResult();
        }
    }
}
