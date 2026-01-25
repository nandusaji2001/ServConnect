using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace ServConnect.Services
{
    public interface IIdVerificationService
    {
        Task<IdVerificationResult> VerifyIdentityAsync(string userName, string imagePath);
        Task<IdVerificationResult> VerifyIdentityBase64Async(string userName, string imageBase64);
        bool IsServiceAvailable { get; }
    }

    public class IdVerificationResult
    {
        public bool Verified { get; set; }
        public bool AutoApproved { get; set; }
        public double SimilarityScore { get; set; }
        public double Threshold { get; set; }
        public string? UserName { get; set; }
        public List<string>? ExtractedNames { get; set; }
        public IdVerificationMatch? BestMatch { get; set; }
        public string? Message { get; set; }
        public string? Error { get; set; }
    }

    public class IdVerificationMatch
    {
        public string? ExtractedName { get; set; }
        public double Similarity { get; set; }
        public double OcrConfidence { get; set; }
        public string? Source { get; set; }
    }

    public class IdVerificationService : IIdVerificationService
    {
        private readonly HttpClient _httpClient;
        private readonly ILogger<IdVerificationService> _logger;
        private readonly string _apiBaseUrl;
        private readonly double _threshold;
        private bool _isAvailable = true;
        private DateTime _lastHealthCheck = DateTime.MinValue;

        public bool IsServiceAvailable => _isAvailable;

        public IdVerificationService(IConfiguration config, ILogger<IdVerificationService> logger)
        {
            _logger = logger;
            _apiBaseUrl = config["IdVerification:ApiUrl"] ?? "http://localhost:5004";
            _threshold = double.Parse(config["IdVerification:Threshold"] ?? "0.75");
            
            _httpClient = new HttpClient
            {
                Timeout = TimeSpan.FromSeconds(60) // OCR can take some time
            };
        }

        private async Task CheckServiceHealthAsync()
        {
            // Check health every 30 seconds
            if ((DateTime.UtcNow - _lastHealthCheck).TotalSeconds < 30)
                return;

            try
            {
                var response = await _httpClient.GetAsync($"{_apiBaseUrl}/health");
                _isAvailable = response.IsSuccessStatusCode;
                _lastHealthCheck = DateTime.UtcNow;
                
                if (_isAvailable)
                {
                    _logger.LogInformation("ID Verification service is available");
                }
                else
                {
                    _logger.LogWarning("ID Verification service returned non-success status");
                }
            }
            catch (Exception ex)
            {
                _isAvailable = false;
                _lastHealthCheck = DateTime.UtcNow;
                _logger.LogWarning("ID Verification service health check failed: {Error}", ex.Message);
            }
        }

        public async Task<IdVerificationResult> VerifyIdentityAsync(string userName, string imagePath)
        {
            if (string.IsNullOrWhiteSpace(userName) || string.IsNullOrWhiteSpace(imagePath))
            {
                return new IdVerificationResult
                {
                    Verified = false,
                    AutoApproved = false,
                    Message = "User name or image path is missing",
                    Error = "Invalid input"
                };
            }

            // Check if it's a web path, we need to read the file and convert to base64
            if (imagePath.StartsWith("/"))
            {
                // This is a relative web path, we need to get the absolute path
                // or read the file content and use base64
                return new IdVerificationResult
                {
                    Verified = false,
                    AutoApproved = false,
                    Message = "Image path verification requires base64 encoding",
                    Error = "Use VerifyIdentityBase64Async for web paths"
                };
            }

            await CheckServiceHealthAsync();

            if (!_isAvailable)
            {
                _logger.LogWarning("ID Verification service unavailable");
                return new IdVerificationResult
                {
                    Verified = false,
                    AutoApproved = false,
                    Message = "ID verification service is temporarily unavailable. Your ID will be reviewed by an admin.",
                    Error = "Service unavailable"
                };
            }

            try
            {
                var requestBody = new
                {
                    user_name = userName,
                    image_path = imagePath,
                    threshold = _threshold
                };

                var json = JsonSerializer.Serialize(requestBody);
                var content = new StringContent(json, Encoding.UTF8, "application/json");

                var response = await _httpClient.PostAsync($"{_apiBaseUrl}/verify", content);
                
                if (response.IsSuccessStatusCode)
                {
                    var responseJson = await response.Content.ReadAsStringAsync();
                    _logger.LogInformation("ID Verification API Response: {Response}", responseJson);
                    
                    var result = JsonSerializer.Deserialize<IdVerificationApiResponse>(responseJson, 
                        new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

                    return MapApiResponse(result);
                }
                else
                {
                    var errorContent = await response.Content.ReadAsStringAsync();
                    _logger.LogError("ID Verification API error: {StatusCode} - {Error}", 
                        response.StatusCode, errorContent);
                    
                    return new IdVerificationResult
                    {
                        Verified = false,
                        AutoApproved = false,
                        Message = "Could not verify ID automatically. Your ID will be reviewed by an admin.",
                        Error = $"API error: {response.StatusCode}"
                    };
                }
            }
            catch (TaskCanceledException)
            {
                _logger.LogWarning("ID Verification API request timed out");
                return new IdVerificationResult
                {
                    Verified = false,
                    AutoApproved = false,
                    Message = "ID verification timed out. Your ID will be reviewed by an admin.",
                    Error = "Request timed out"
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error calling ID Verification API");
                return new IdVerificationResult
                {
                    Verified = false,
                    AutoApproved = false,
                    Message = "Error during ID verification. Your ID will be reviewed by an admin.",
                    Error = ex.Message
                };
            }
        }

        public async Task<IdVerificationResult> VerifyIdentityBase64Async(string userName, string imageBase64)
        {
            if (string.IsNullOrWhiteSpace(userName) || string.IsNullOrWhiteSpace(imageBase64))
            {
                return new IdVerificationResult
                {
                    Verified = false,
                    AutoApproved = false,
                    Message = "User name or image data is missing",
                    Error = "Invalid input"
                };
            }

            await CheckServiceHealthAsync();

            if (!_isAvailable)
            {
                _logger.LogWarning("ID Verification service unavailable");
                return new IdVerificationResult
                {
                    Verified = false,
                    AutoApproved = false,
                    Message = "ID verification service is temporarily unavailable. Your ID will be reviewed by an admin.",
                    Error = "Service unavailable"
                };
            }

            try
            {
                var requestBody = new
                {
                    user_name = userName,
                    image_base64 = imageBase64,
                    threshold = _threshold
                };

                var json = JsonSerializer.Serialize(requestBody);
                var content = new StringContent(json, Encoding.UTF8, "application/json");

                _logger.LogInformation("Sending ID verification request for user: {UserName}", userName);
                
                var response = await _httpClient.PostAsync($"{_apiBaseUrl}/verify", content);
                
                if (response.IsSuccessStatusCode)
                {
                    var responseJson = await response.Content.ReadAsStringAsync();
                    _logger.LogInformation("ID Verification API Response: {Response}", responseJson);
                    
                    var result = JsonSerializer.Deserialize<IdVerificationApiResponse>(responseJson, 
                        new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

                    return MapApiResponse(result);
                }
                else
                {
                    var errorContent = await response.Content.ReadAsStringAsync();
                    _logger.LogError("ID Verification API error: {StatusCode} - {Error}", 
                        response.StatusCode, errorContent);
                    
                    return new IdVerificationResult
                    {
                        Verified = false,
                        AutoApproved = false,
                        Message = "Could not verify ID automatically. Your ID will be reviewed by an admin.",
                        Error = $"API error: {response.StatusCode}"
                    };
                }
            }
            catch (TaskCanceledException)
            {
                _logger.LogWarning("ID Verification API request timed out");
                return new IdVerificationResult
                {
                    Verified = false,
                    AutoApproved = false,
                    Message = "ID verification timed out. Your ID will be reviewed by an admin.",
                    Error = "Request timed out"
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error calling ID Verification API");
                return new IdVerificationResult
                {
                    Verified = false,
                    AutoApproved = false,
                    Message = "Error during ID verification. Your ID will be reviewed by an admin.",
                    Error = ex.Message
                };
            }
        }

        private IdVerificationResult MapApiResponse(IdVerificationApiResponse? apiResponse)
        {
            if (apiResponse == null)
            {
                return new IdVerificationResult
                {
                    Verified = false,
                    AutoApproved = false,
                    Message = "Invalid response from verification service",
                    Error = "Null response"
                };
            }

            return new IdVerificationResult
            {
                Verified = apiResponse.Verified,
                AutoApproved = apiResponse.AutoApproved,
                SimilarityScore = apiResponse.SimilarityScore,
                Threshold = apiResponse.Threshold,
                UserName = apiResponse.UserName,
                ExtractedNames = apiResponse.ExtractedNames,
                BestMatch = apiResponse.BestMatch != null ? new IdVerificationMatch
                {
                    ExtractedName = apiResponse.BestMatch.ExtractedName,
                    Similarity = apiResponse.BestMatch.Similarity,
                    OcrConfidence = apiResponse.BestMatch.OcrConfidence,
                    Source = apiResponse.BestMatch.Source
                } : null,
                Message = apiResponse.Message,
                Error = apiResponse.Error
            };
        }
    }

    // Internal class for deserializing API response
    internal class IdVerificationApiResponse
    {
        [JsonPropertyName("verified")]
        public bool Verified { get; set; }
        
        [JsonPropertyName("auto_approved")]
        public bool AutoApproved { get; set; }
        
        [JsonPropertyName("similarity_score")]
        public double SimilarityScore { get; set; }
        
        [JsonPropertyName("threshold")]
        public double Threshold { get; set; }
        
        [JsonPropertyName("user_name")]
        public string? UserName { get; set; }
        
        [JsonPropertyName("extracted_names")]
        public List<string>? ExtractedNames { get; set; }
        
        [JsonPropertyName("best_match")]
        public IdVerificationMatchResponse? BestMatch { get; set; }
        
        [JsonPropertyName("message")]
        public string? Message { get; set; }
        
        [JsonPropertyName("error")]
        public string? Error { get; set; }
    }

    internal class IdVerificationMatchResponse
    {
        [JsonPropertyName("extracted_name")]
        public string? ExtractedName { get; set; }
        
        [JsonPropertyName("similarity")]
        public double Similarity { get; set; }
        
        [JsonPropertyName("ocr_confidence")]
        public double OcrConfidence { get; set; }
        
        [JsonPropertyName("source")]
        public string? Source { get; set; }
    }
}
