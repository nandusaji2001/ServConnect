using System.Text;
using System.Text.Json;

namespace ServConnect.Services
{
    public interface IContentModerationService
    {
        Task<ContentModerationResult> AnalyzeContentAsync(string text);
        Task<List<ContentModerationResult>> AnalyzeContentBatchAsync(List<string> texts);
        bool IsServiceAvailable { get; }
    }

    public class ContentModerationResult
    {
        public bool IsHarmful { get; set; }
        public double Confidence { get; set; }
        public double Threshold { get; set; }
        public string? OriginalText { get; set; }
    }

    public class ContentModerationService : IContentModerationService
    {
        private readonly HttpClient _httpClient;
        private readonly ILogger<ContentModerationService> _logger;
        private readonly string _apiBaseUrl;
        private readonly double _threshold;
        private bool _isAvailable = true;
        private DateTime _lastHealthCheck = DateTime.MinValue;

        public bool IsServiceAvailable => _isAvailable;

        public ContentModerationService(IConfiguration config, ILogger<ContentModerationService> logger)
        {
            _logger = logger;
            _apiBaseUrl = config["ContentModeration:ApiUrl"] ?? "http://localhost:5050";
            _threshold = double.Parse(config["ContentModeration:Threshold"] ?? "0.5");
            
            _httpClient = new HttpClient
            {
                Timeout = TimeSpan.FromSeconds(5)
            };
        }

        public async Task<ContentModerationResult> AnalyzeContentAsync(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                return new ContentModerationResult
                {
                    IsHarmful = false,
                    Confidence = 0,
                    Threshold = _threshold,
                    OriginalText = text
                };
            }

            // Check service availability periodically
            await CheckServiceHealthAsync();

            if (!_isAvailable)
            {
                _logger.LogWarning("Content moderation service unavailable, skipping check");
                return new ContentModerationResult
                {
                    IsHarmful = false,
                    Confidence = 0,
                    Threshold = _threshold,
                    OriginalText = text
                };
            }

            try
            {
                var requestBody = new
                {
                    text = text,
                    threshold = _threshold
                };

                var json = JsonSerializer.Serialize(requestBody);
                var content = new StringContent(json, Encoding.UTF8, "application/json");

                var response = await _httpClient.PostAsync($"{_apiBaseUrl}/predict", content);
                
                if (response.IsSuccessStatusCode)
                {
                    var responseJson = await response.Content.ReadAsStringAsync();
                    _logger.LogInformation("ML API Response: {Response}", responseJson);
                    
                    var result = JsonSerializer.Deserialize<ModerationApiResponse>(responseJson, 
                        new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

                    _logger.LogInformation("Parsed result - IsHarmful: {IsHarmful}, Confidence: {Confidence}", 
                        result?.IsHarmful, result?.Confidence);

                    return new ContentModerationResult
                    {
                        IsHarmful = result?.IsHarmful ?? false,
                        Confidence = result?.Confidence ?? 0,
                        Threshold = result?.Threshold ?? _threshold,
                        OriginalText = text
                    };
                }
                else
                {
                    _logger.LogWarning("Content moderation API returned {StatusCode}", response.StatusCode);
                }
            }
            catch (TaskCanceledException)
            {
                _logger.LogWarning("Content moderation API request timed out");
                _isAvailable = false;
            }
            catch (HttpRequestException ex)
            {
                _logger.LogWarning(ex, "Content moderation API request failed");
                _isAvailable = false;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error in content moderation");
            }

            return new ContentModerationResult
            {
                IsHarmful = false,
                Confidence = 0,
                Threshold = _threshold,
                OriginalText = text
            };
        }

        public async Task<List<ContentModerationResult>> AnalyzeContentBatchAsync(List<string> texts)
        {
            if (texts == null || !texts.Any())
            {
                return new List<ContentModerationResult>();
            }

            await CheckServiceHealthAsync();

            if (!_isAvailable)
            {
                return texts.Select(t => new ContentModerationResult
                {
                    IsHarmful = false,
                    Confidence = 0,
                    Threshold = _threshold,
                    OriginalText = t
                }).ToList();
            }

            try
            {
                var requestBody = new
                {
                    texts = texts,
                    threshold = _threshold
                };

                var json = JsonSerializer.Serialize(requestBody);
                var content = new StringContent(json, Encoding.UTF8, "application/json");

                var response = await _httpClient.PostAsync($"{_apiBaseUrl}/predict/batch", content);
                
                if (response.IsSuccessStatusCode)
                {
                    var responseJson = await response.Content.ReadAsStringAsync();
                    var result = JsonSerializer.Deserialize<BatchModerationApiResponse>(responseJson,
                        new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

                    if (result?.Results != null)
                    {
                        return result.Results.Select((r, i) => new ContentModerationResult
                        {
                            IsHarmful = r.IsHarmful,
                            Confidence = r.Confidence,
                            Threshold = result.Threshold,
                            OriginalText = i < texts.Count ? texts[i] : null
                        }).ToList();
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Batch content moderation failed");
            }

            return texts.Select(t => new ContentModerationResult
            {
                IsHarmful = false,
                Confidence = 0,
                Threshold = _threshold,
                OriginalText = t
            }).ToList();
        }

        private async Task CheckServiceHealthAsync()
        {
            // Only check health every 30 seconds
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

        private class ModerationApiResponse
        {
            [System.Text.Json.Serialization.JsonPropertyName("is_harmful")]
            public bool IsHarmful { get; set; }
            
            [System.Text.Json.Serialization.JsonPropertyName("confidence")]
            public double Confidence { get; set; }
            
            [System.Text.Json.Serialization.JsonPropertyName("threshold")]
            public double Threshold { get; set; }
        }

        private class BatchModerationApiResponse
        {
            [System.Text.Json.Serialization.JsonPropertyName("results")]
            public List<BatchResultItem> Results { get; set; } = new();
            
            [System.Text.Json.Serialization.JsonPropertyName("threshold")]
            public double Threshold { get; set; }
        }

        private class BatchResultItem
        {
            [System.Text.Json.Serialization.JsonPropertyName("text")]
            public string? Text { get; set; }
            
            [System.Text.Json.Serialization.JsonPropertyName("is_harmful")]
            public bool IsHarmful { get; set; }
            
            [System.Text.Json.Serialization.JsonPropertyName("confidence")]
            public double Confidence { get; set; }
        }
    }
}
