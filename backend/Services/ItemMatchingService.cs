using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using ServConnect.Models;

namespace ServConnect.Services
{
    public interface IItemMatchingService
    {
        Task<List<ItemMatch>> FindMatchingLostItemsAsync(LostFoundItem foundItem, List<LostItemReport> lostReports, double threshold = 0.5);
        Task<List<ItemMatch>> FindMatchingFoundItemsAsync(LostItemReport lostReport, List<LostFoundItem> foundItems, double threshold = 0.5);
        Task<double> ComputeSimilarityAsync(object item1, object item2);
        bool IsServiceAvailable { get; }
    }

    public class ItemMatch
    {
        public string ItemId { get; set; } = string.Empty;
        public string UserId { get; set; } = string.Empty;
        public string Title { get; set; } = string.Empty;
        public double Similarity { get; set; }
        public double MatchPercentage { get; set; }
    }

    public class ItemMatchingService : IItemMatchingService
    {
        private readonly HttpClient _httpClient;
        private readonly ILogger<ItemMatchingService> _logger;
        private readonly string _apiBaseUrl;
        private bool _isAvailable = true;
        private DateTime _lastHealthCheck = DateTime.MinValue;

        public bool IsServiceAvailable => _isAvailable;

        public ItemMatchingService(IConfiguration config, ILogger<ItemMatchingService> logger)
        {
            _logger = logger;
            _apiBaseUrl = config["ItemMatching:ApiUrl"] ?? "http://localhost:5003";

            _httpClient = new HttpClient
            {
                Timeout = TimeSpan.FromSeconds(30) // S-BERT can take a bit longer
            };
        }

        public async Task<List<ItemMatch>> FindMatchingLostItemsAsync(LostFoundItem foundItem, List<LostItemReport> lostReports, double threshold = 0.5)
        {
            if (lostReports == null || !lostReports.Any())
            {
                return new List<ItemMatch>();
            }

            await CheckServiceHealthAsync();

            if (!_isAvailable)
            {
                _logger.LogWarning("Item matching service unavailable");
                return new List<ItemMatch>();
            }

            try
            {
                var queryItem = new
                {
                    id = foundItem.Id,
                    title = foundItem.Title,
                    category = foundItem.Category,
                    description = foundItem.Description,
                    location = foundItem.FoundLocation
                };

                var candidateItems = lostReports.Select(r => new
                {
                    id = r.Id,
                    user_id = r.LostByUserId.ToString(),
                    title = r.Title,
                    category = r.Category,
                    description = r.Description,
                    location = r.LostLocation
                }).ToList();

                var requestBody = new
                {
                    query_item = queryItem,
                    candidate_items = candidateItems,
                    threshold = threshold,
                    top_k = 10
                };

                var json = JsonSerializer.Serialize(requestBody);
                var content = new StringContent(json, Encoding.UTF8, "application/json");

                var response = await _httpClient.PostAsync($"{_apiBaseUrl}/match", content);

                if (response.IsSuccessStatusCode)
                {
                    var responseJson = await response.Content.ReadAsStringAsync();
                    var result = JsonSerializer.Deserialize<MatchApiResponse>(responseJson,
                        new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

                    if (result?.Success == true && result.Matches != null)
                    {
                        return result.Matches.Select(m => new ItemMatch
                        {
                            ItemId = m.Item?.Id ?? string.Empty,
                            UserId = m.Item?.UserId ?? string.Empty,
                            Title = m.Item?.Title ?? string.Empty,
                            Similarity = m.Similarity,
                            MatchPercentage = m.MatchPercentage
                        }).ToList();
                    }
                }
                else
                {
                    _logger.LogWarning("Item matching API returned {StatusCode}", response.StatusCode);
                }
            }
            catch (TaskCanceledException)
            {
                _logger.LogWarning("Item matching API request timed out");
                _isAvailable = false;
            }
            catch (HttpRequestException ex)
            {
                _logger.LogWarning(ex, "Item matching API request failed");
                _isAvailable = false;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error in item matching");
            }

            return new List<ItemMatch>();
        }

        public async Task<List<ItemMatch>> FindMatchingFoundItemsAsync(LostItemReport lostReport, List<LostFoundItem> foundItems, double threshold = 0.5)
        {
            if (foundItems == null || !foundItems.Any())
            {
                return new List<ItemMatch>();
            }

            await CheckServiceHealthAsync();

            if (!_isAvailable)
            {
                _logger.LogWarning("Item matching service unavailable");
                return new List<ItemMatch>();
            }

            try
            {
                var queryItem = new
                {
                    id = lostReport.Id,
                    title = lostReport.Title,
                    category = lostReport.Category,
                    description = lostReport.Description,
                    location = lostReport.LostLocation
                };

                var candidateItems = foundItems.Select(f => new
                {
                    id = f.Id,
                    user_id = f.FoundByUserId.ToString(),
                    title = f.Title,
                    category = f.Category,
                    description = f.Description,
                    location = f.FoundLocation
                }).ToList();

                var requestBody = new
                {
                    query_item = queryItem,
                    candidate_items = candidateItems,
                    threshold = threshold,
                    top_k = 10
                };

                var json = JsonSerializer.Serialize(requestBody);
                var content = new StringContent(json, Encoding.UTF8, "application/json");

                var response = await _httpClient.PostAsync($"{_apiBaseUrl}/match", content);

                if (response.IsSuccessStatusCode)
                {
                    var responseJson = await response.Content.ReadAsStringAsync();
                    var result = JsonSerializer.Deserialize<MatchApiResponse>(responseJson,
                        new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

                    if (result?.Success == true && result.Matches != null)
                    {
                        return result.Matches.Select(m => new ItemMatch
                        {
                            ItemId = m.Item?.Id ?? string.Empty,
                            UserId = m.Item?.UserId ?? string.Empty,
                            Title = m.Item?.Title ?? string.Empty,
                            Similarity = m.Similarity,
                            MatchPercentage = m.MatchPercentage
                        }).ToList();
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error finding matching found items");
            }

            return new List<ItemMatch>();
        }

        public async Task<double> ComputeSimilarityAsync(object item1, object item2)
        {
            await CheckServiceHealthAsync();

            if (!_isAvailable)
            {
                return 0.0;
            }

            try
            {
                var requestBody = new { item1, item2 };
                var json = JsonSerializer.Serialize(requestBody);
                var content = new StringContent(json, Encoding.UTF8, "application/json");

                var response = await _httpClient.PostAsync($"{_apiBaseUrl}/similarity", content);

                if (response.IsSuccessStatusCode)
                {
                    var responseJson = await response.Content.ReadAsStringAsync();
                    var result = JsonSerializer.Deserialize<SimilarityApiResponse>(responseJson,
                        new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

                    return result?.Similarity ?? 0.0;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error computing similarity");
            }

            return 0.0;
        }

        private async Task CheckServiceHealthAsync()
        {
            if ((DateTime.UtcNow - _lastHealthCheck).TotalSeconds < 30)
            {
                return;
            }

            _lastHealthCheck = DateTime.UtcNow;

            try
            {
                var response = await _httpClient.GetAsync($"{_apiBaseUrl}/health");
                _isAvailable = response.IsSuccessStatusCode;
            }
            catch
            {
                _isAvailable = false;
            }
        }

        #region API Response Classes

        private class MatchApiResponse
        {
            [JsonPropertyName("success")]
            public bool Success { get; set; }

            [JsonPropertyName("matches")]
            public List<MatchResult>? Matches { get; set; }

            [JsonPropertyName("query_item_id")]
            public string? QueryItemId { get; set; }
        }

        private class MatchResult
        {
            [JsonPropertyName("item")]
            public MatchedItem? Item { get; set; }

            [JsonPropertyName("similarity")]
            public double Similarity { get; set; }

            [JsonPropertyName("match_percentage")]
            public double MatchPercentage { get; set; }
        }

        private class MatchedItem
        {
            [JsonPropertyName("id")]
            public string? Id { get; set; }

            [JsonPropertyName("user_id")]
            public string? UserId { get; set; }

            [JsonPropertyName("title")]
            public string? Title { get; set; }

            [JsonPropertyName("category")]
            public string? Category { get; set; }

            [JsonPropertyName("description")]
            public string? Description { get; set; }

            [JsonPropertyName("location")]
            public string? Location { get; set; }
        }

        private class SimilarityApiResponse
        {
            [JsonPropertyName("success")]
            public bool Success { get; set; }

            [JsonPropertyName("similarity")]
            public double Similarity { get; set; }

            [JsonPropertyName("match_percentage")]
            public double MatchPercentage { get; set; }
        }

        #endregion
    }
}
