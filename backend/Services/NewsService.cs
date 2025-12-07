using System.Text.Json;
using System.Text.Json.Serialization;

namespace ServConnect.Services
{
    public class NewsService : INewsService
    {
        private readonly HttpClient _httpClient;
        private readonly ILogger<NewsService> _logger;
        private readonly IConfiguration _configuration;

        public NewsService(HttpClient httpClient, ILogger<NewsService> logger, IConfiguration configuration)
        {
            _httpClient = httpClient;
            _logger = logger;
            _configuration = configuration;
        }

        public async Task<NewsResponse> GetNewsByLocationAsync(string location, string language = "en", string country = "in")
        {
            var response = new NewsResponse();

            try
            {
                var apiKey = _configuration["NewsDataIo:ApiKey"];
                if (string.IsNullOrEmpty(apiKey))
                {
                    _logger.LogWarning("NewsData.io API key is not configured");
                    return response;
                }

                var urlBuilder = new System.Text.StringBuilder("https://newsdata.io/api/1/news?");
                urlBuilder.Append($"apikey={apiKey}");
                urlBuilder.Append($"&country={Uri.EscapeDataString(country)}");
                urlBuilder.Append($"&language={Uri.EscapeDataString(language)}");
                urlBuilder.Append($"&q={Uri.EscapeDataString(location)}");
                
                var url = urlBuilder.ToString();
                var httpResponse = await _httpClient.GetAsync(url);
                
                if (!httpResponse.IsSuccessStatusCode)
                {
                    _logger.LogWarning($"Failed to fetch news for location: {location}, country: {country}, language: {language}. Status: {httpResponse.StatusCode}");
                    return response;
                }

                var content = await httpResponse.Content.ReadAsStringAsync();
                var newsDataResponse = JsonSerializer.Deserialize<NewsDataResponse>(content, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

                if (newsDataResponse?.Results != null)
                {
                    foreach (var item in newsDataResponse.Results.Take(8))
                    {
                        try
                        {
                            if (string.IsNullOrEmpty(item.Title) && string.IsNullOrEmpty(item.Description))
                                continue;

                            DateTime? pubDate = null;
                            if (!string.IsNullOrEmpty(item.PubDate) && DateTime.TryParse(item.PubDate, out var parsed))
                            {
                                pubDate = parsed;
                            }

                            var article = new NewsArticle
                            {
                                Title = item.Title ?? string.Empty,
                                Description = item.Description ?? string.Empty,
                                Link = item.Link ?? string.Empty,
                                Source = item.SourceId ?? "NewsData.io",
                                ImageUrl = item.ImageUrl ?? string.Empty,
                                PubDate = pubDate
                            };

                            response.Articles.Add(article);
                        }
                        catch (Exception ex)
                        {
                            _logger.LogWarning($"Error parsing news item: {ex.Message}");
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error fetching news for location {location}, country: {country}, language {language}: {ex.Message}");
            }

            return response;
        }

        public async Task<NewsResponse> GetNewsByLocationAsync(string location, string language)
        {
            return await GetNewsByLocationAsync(location, language, "in");
        }

        public async Task<NewsResponse> GetNewsByLocationAsync(string location)
        {
            return await GetNewsByLocationAsync(location, "en", "in");
        }

        // Helper classes for NewsData.io API response
        private class NewsDataResponse
        {
            [JsonPropertyName("results")]
            public NewsDataResult[]? Results { get; set; }
            
            [JsonPropertyName("status")]
            public string? Status { get; set; }
            
            [JsonPropertyName("totalResults")]
            public int TotalResults { get; set; }
        }

        private class NewsDataResult
        {
            [JsonPropertyName("title")]
            public string? Title { get; set; }
            
            [JsonPropertyName("description")]
            public string? Description { get; set; }
            
            [JsonPropertyName("content")]
            public string? Content { get; set; }
            
            [JsonPropertyName("link")]
            public string? Link { get; set; }
            
            [JsonPropertyName("pubDate")]
            public string? PubDate { get; set; }
            
            [JsonPropertyName("source_id")]
            public string? SourceId { get; set; }
            
            [JsonPropertyName("image_url")]
            public string? ImageUrl { get; set; }
            
            [JsonPropertyName("keywords")]
            public string?[]? Keywords { get; set; }
        }
    }
}