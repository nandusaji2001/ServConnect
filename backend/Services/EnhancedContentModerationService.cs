using System.Text;
using System.Text.Json;

namespace ServConnect.Services
{
    public interface IEnhancedContentModerationService
    {
        Task<EnhancedModerationResult> AnalyzeContentWithImageAsync(string caption, List<byte[]> images, string? userId = null);
        Task<string> ExtractTextFromImageAsync(byte[] imageData);
    }

    public class EnhancedModerationResult
    {
        public bool IsHarmful { get; set; }
        public double Confidence { get; set; }
        public string? CaptionText { get; set; }
        public List<string> ExtractedTexts { get; set; } = new();
        public string CombinedText { get; set; } = string.Empty;
        public string Reason { get; set; } = string.Empty;
    }

    public class EnhancedContentModerationService : IEnhancedContentModerationService
    {
        private readonly HttpClient _httpClient;
        private readonly ILogger<EnhancedContentModerationService> _logger;
        private readonly string _moderationApiUrl;
        private readonly string _ocrApiUrl;
        private readonly double _threshold;

        public EnhancedContentModerationService(
            IConfiguration config, 
            ILogger<EnhancedContentModerationService> logger)
        {
            _logger = logger;
            _moderationApiUrl = config["ContentModeration:ApiUrl"] ?? "http://localhost:5050";
            _ocrApiUrl = config["ContentModeration:OcrApiUrl"] ?? "http://localhost:5008";
            _threshold = double.Parse(config["ContentModeration:Threshold"] ?? "0.5");
            
            _httpClient = new HttpClient
            {
                Timeout = TimeSpan.FromSeconds(30) // Longer timeout for OCR
            };
        }

        public async Task<string> ExtractTextFromImageAsync(byte[] imageData)
        {
            try
            {
                var requestBody = new
                {
                    image = Convert.ToBase64String(imageData)
                };

                var json = JsonSerializer.Serialize(requestBody);
                var content = new StringContent(json, Encoding.UTF8, "application/json");

                var response = await _httpClient.PostAsync($"{_ocrApiUrl}/extract-text", content);
                
                if (response.IsSuccessStatusCode)
                {
                    var responseJson = await response.Content.ReadAsStringAsync();
                    var result = JsonSerializer.Deserialize<OcrResponse>(responseJson, 
                        new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

                    return result?.Text ?? string.Empty;
                }
                else
                {
                    _logger.LogWarning("OCR API returned {StatusCode}", response.StatusCode);
                    return string.Empty;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error extracting text from image");
                return string.Empty;
            }
        }

        public async Task<EnhancedModerationResult> AnalyzeContentWithImageAsync(
            string caption, 
            List<byte[]> images,
            string? userId = null)
        {
            var result = new EnhancedModerationResult
            {
                CaptionText = caption
            };

            try
            {
                // Use intelligent moderation API with built-in OCR
                // Send first image (or combine multiple if needed)
                var imageBase64 = images.Any() ? Convert.ToBase64String(images[0]) : null;
                
                _logger.LogInformation("Sending to intelligent API - Caption: '{Caption}', HasImage: {HasImage}", 
                    caption, imageBase64 != null);
                
                var requestBody = new
                {
                    text = caption ?? string.Empty,
                    image = imageBase64,
                    user_id = userId
                };

                var json = JsonSerializer.Serialize(requestBody);
                var content = new StringContent(json, Encoding.UTF8, "application/json");

                var response = await _httpClient.PostAsync($"{_moderationApiUrl}/analyze/content", content);
                
                _logger.LogInformation("Intelligent API Status: {StatusCode}", response.StatusCode);
                
                if (response.IsSuccessStatusCode)
                {
                    var responseJson = await response.Content.ReadAsStringAsync();
                    _logger.LogInformation("Intelligent API Response: {Response}", responseJson);
                    
                    var apiResult = JsonSerializer.Deserialize<IntelligentModerationApiResponse>(responseJson, 
                        new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

                    if (apiResult != null)
                    {
                        result.IsHarmful = apiResult.IsHarmful;
                        result.Confidence = apiResult.FinalRiskScore;
                        result.CombinedText = caption ?? string.Empty;
                        result.Reason = apiResult.Reason ?? "Content analysis completed";
                        
                        // Add OCR extracted text if available
                        if (!string.IsNullOrWhiteSpace(apiResult.OcrText))
                        {
                            result.ExtractedTexts.Add(apiResult.OcrText);
                            result.CombinedText = $"{caption} {apiResult.OcrText}".Trim();
                        }

                        _logger.LogInformation(
                            "Enhanced moderation result: Caption='{Caption}', OcrText='{OcrText}', TextToxicity={TextTox}, OcrToxicity={OcrTox}, ImageRisk={ImgRisk}, FinalRisk={FinalRisk}, IsHarmful={IsHarmful}, Recommendation={Rec}",
                            caption, apiResult.OcrText, apiResult.TextToxicityScore, apiResult.OcrToxicityScore, 
                            apiResult.ImageRiskScore, apiResult.FinalRiskScore, result.IsHarmful, apiResult.Recommendation);

                        return result;
                    }
                }
                else
                {
                    var errorContent = await response.Content.ReadAsStringAsync();
                    _logger.LogWarning("Intelligent moderation API returned {StatusCode}: {Error}", 
                        response.StatusCode, errorContent);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in enhanced content moderation");
            }

            // Fallback: analyze caption only
            if (!string.IsNullOrWhiteSpace(caption))
            {
                var fallbackResult = await AnalyzeTextAsync(caption);
                result.IsHarmful = fallbackResult.IsHarmful;
                result.Confidence = fallbackResult.Confidence;
                result.Reason = "Analyzed caption only (API error)";
            }
            
            return result;
        }

        private async Task<ModerationResult> AnalyzeTextAsync(string text)
        {
            try
            {
                var requestBody = new
                {
                    text = text
                };

                var json = JsonSerializer.Serialize(requestBody);
                var content = new StringContent(json, Encoding.UTF8, "application/json");

                var response = await _httpClient.PostAsync($"{_moderationApiUrl}/analyze/content", content);
                
                if (response.IsSuccessStatusCode)
                {
                    var responseJson = await response.Content.ReadAsStringAsync();
                    var result = JsonSerializer.Deserialize<IntelligentModerationApiResponse>(responseJson, 
                        new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

                    return new ModerationResult
                    {
                        IsHarmful = result?.IsHarmful ?? false,
                        Confidence = result?.FinalRiskScore ?? 0
                    };
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error analyzing text");
            }

            return new ModerationResult { IsHarmful = false, Confidence = 0 };
        }

        private class ModerationResult
        {
            public bool IsHarmful { get; set; }
            public double Confidence { get; set; }
        }

        private class ModerationApiResponse
        {
            [System.Text.Json.Serialization.JsonPropertyName("is_harmful")]
            public bool IsHarmful { get; set; }
            
            [System.Text.Json.Serialization.JsonPropertyName("confidence")]
            public double Confidence { get; set; }
        }

        private class IntelligentModerationApiResponse
        {
            [System.Text.Json.Serialization.JsonPropertyName("text_toxicity_score")]
            public double TextToxicityScore { get; set; }
            
            [System.Text.Json.Serialization.JsonPropertyName("image_risk_score")]
            public double ImageRiskScore { get; set; }
            
            [System.Text.Json.Serialization.JsonPropertyName("ocr_text")]
            public string? OcrText { get; set; }
            
            [System.Text.Json.Serialization.JsonPropertyName("ocr_toxicity_score")]
            public double OcrToxicityScore { get; set; }
            
            [System.Text.Json.Serialization.JsonPropertyName("user_trust_score")]
            public double UserTrustScore { get; set; }
            
            [System.Text.Json.Serialization.JsonPropertyName("post_risk_score")]
            public double PostRiskScore { get; set; }
            
            [System.Text.Json.Serialization.JsonPropertyName("final_risk_score")]
            public double FinalRiskScore { get; set; }
            
            [System.Text.Json.Serialization.JsonPropertyName("is_harmful")]
            public bool IsHarmful { get; set; }
            
            [System.Text.Json.Serialization.JsonPropertyName("recommendation")]
            public string? Recommendation { get; set; }
            
            [System.Text.Json.Serialization.JsonPropertyName("reason")]
            public string? Reason { get; set; }
        }

        private class OcrResponse
        {
            [System.Text.Json.Serialization.JsonPropertyName("text")]
            public string Text { get; set; } = string.Empty;
            
            [System.Text.Json.Serialization.JsonPropertyName("confidence")]
            public double Confidence { get; set; }
            
            [System.Text.Json.Serialization.JsonPropertyName("has_text")]
            public bool HasText { get; set; }
        }
    }
}
