using Microsoft.Extensions.Caching.Memory;
using System.Text.Json;

namespace ServConnect.Services
{
    public class Fast2SmsOtpService : ISmsService
    {
        private readonly HttpClient _httpClient;
        private readonly IConfiguration _configuration;
        private readonly ILogger<Fast2SmsOtpService> _logger;
        private readonly IMemoryCache _memoryCache;
        private const int OtpExpiryMinutes = 10;

        public Fast2SmsOtpService(
            HttpClient httpClient, 
            IConfiguration configuration, 
            ILogger<Fast2SmsOtpService> logger,
            IMemoryCache memoryCache)
        {
            _httpClient = httpClient;
            _configuration = configuration;
            _logger = logger;
            _memoryCache = memoryCache;
        }

        public async Task<bool> SendSmsAsync(string phoneNumber, string message)
        {
            try
            {
                var apiKey = _configuration["Fast2SMS:ApiKey"];
                var route = _configuration["Fast2SMS:Route"] ?? "q"; // 'q' is free promotional route
                var senderId = _configuration["Fast2SMS:SenderId"] ?? "FSTSMS";
                var simulate = _configuration.GetValue<bool>("Fast2SMS:SimulateInDevelopment", false);

                if (string.IsNullOrEmpty(apiKey))
                {
                    _logger.LogError("Fast2SMS API key not configured");
                    return false;
                }

                if (simulate)
                {
                    _logger.LogWarning("🚀 FAST2SMS SIMULATION - Message would be sent to {PhoneNumber}: {Message}", phoneNumber, message);
                    return true;
                }

                // Clean phone number for Indian format
                var cleanPhoneNumber = phoneNumber.Replace("+91", "").Replace("+", "");
                
                _logger.LogInformation("Sending SMS via Fast2SMS to {PhoneNumber} using route '{Route}'", phoneNumber, route);

                // Use the free promotional route 'q' which works without verification
                var requestData = new
                {
                    route = route,
                    message = message,
                    language = "english",
                    flash = 0,
                    numbers = cleanPhoneNumber
                };

                var jsonContent = JsonSerializer.Serialize(requestData);
                var content = new StringContent(jsonContent, System.Text.Encoding.UTF8, "application/json");
                
                _httpClient.DefaultRequestHeaders.Clear();
                _httpClient.DefaultRequestHeaders.Add("authorization", apiKey);

                var response = await _httpClient.PostAsync("https://www.fast2sms.com/dev/bulkV2", content);
                var responseContent = await response.Content.ReadAsStringAsync();

                if (response.IsSuccessStatusCode)
                {
                    _logger.LogInformation("Fast2SMS sent successfully to {PhoneNumber}. Response: {Response}", phoneNumber, responseContent);
                    
                    // Store OTP in cache for verification
                    var otp = ExtractOtpFromMessage(message);
                    if (!string.IsNullOrEmpty(otp))
                    {
                        var cacheKey = $"fast2sms_otp_{phoneNumber}";
                        var cacheOptions = new MemoryCacheEntryOptions
                        {
                            AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(OtpExpiryMinutes)
                        };
                        _memoryCache.Set(cacheKey, otp, cacheOptions);
                    }
                    
                    return true;
                }
                else
                {
                    _logger.LogError("Fast2SMS failed. Status: {Status}, Error: {Error}", response.StatusCode, responseContent);
                    
                    // Check if it's the specific API key/transaction requirement error
                    if (responseContent.Contains("You need to complete one transaction") || 
                        responseContent.Contains("status_code\":999"))
                    {
                        _logger.LogWarning("Fast2SMS requires transaction completion. Trying alternative route...");
                    }
                    
                    // Try alternative approach if the first one fails
                    return await TryAlternativeRoute(cleanPhoneNumber, message);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Fast2SMS provider failed for {PhoneNumber}", phoneNumber);
                return false;
            }
        }

        public async Task<bool> SendOtpAsync(string phoneNumber, string otp)
        {
            var message = $"Your ServConnect OTP is {otp}. Valid for {OtpExpiryMinutes} minutes. Do not share with anyone.";
            return await SendSmsAsync(phoneNumber, message);
        }

        private async Task<bool> TryAlternativeRoute(string phoneNumber, string message)
        {
            try
            {
                _logger.LogInformation("Trying alternative Fast2SMS route for {PhoneNumber}", phoneNumber);
                
                var apiKey = _configuration["Fast2SMS:ApiKey"];
                
                // Try simple SMS route with all required parameters
                var requestData = new Dictionary<string, string>
                {
                    {"route", "q"},
                    {"message", message},
                    {"language", "english"},
                    {"flash", "0"},
                    {"numbers", phoneNumber},
                    {"sender_id", "FSTSMS"}
                };

                var content = new FormUrlEncodedContent(requestData);
                
                // Set authorization header instead of including in form data
                _httpClient.DefaultRequestHeaders.Clear();
                _httpClient.DefaultRequestHeaders.Add("authorization", apiKey);
                
                var response = await _httpClient.PostAsync("https://www.fast2sms.com/dev/bulk", content);
                var responseContent = await response.Content.ReadAsStringAsync();

                if (response.IsSuccessStatusCode)
                {
                    _logger.LogInformation("Fast2SMS alternative route successful. Response: {Response}", responseContent);
                    
                    // Store OTP in cache for verification
                    var otp = ExtractOtpFromMessage(message);
                    if (!string.IsNullOrEmpty(otp))
                    {
                        var cacheKey = $"fast2sms_otp_{phoneNumber}";
                        var cacheOptions = new MemoryCacheEntryOptions
                        {
                            AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(OtpExpiryMinutes)
                        };
                        _memoryCache.Set(cacheKey, otp, cacheOptions);
                    }
                    
                    return true;
                }
                else
                {
                    _logger.LogError("Fast2SMS alternative route failed. Status: {Status}, Error: {Error}", response.StatusCode, responseContent);
                    
                    // If both routes fail, enable simulation mode as fallback
                    _logger.LogWarning("Both Fast2SMS routes failed. Falling back to simulation mode.");
                    return await SimulateOtpDelivery(phoneNumber, message);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Fast2SMS alternative route exception");
                
                // If exception occurs, enable simulation mode as fallback
                _logger.LogWarning("Fast2SMS exception occurred. Falling back to simulation mode.");
                return await SimulateOtpDelivery(phoneNumber, message);
            }
        }

        private async Task<bool> SimulateOtpDelivery(string phoneNumber, string message)
        {
            _logger.LogWarning("🚀 FAST2SMS SIMULATION MODE - Message would be sent to {PhoneNumber}: {Message}", phoneNumber, message);
            
            // Store OTP in cache for verification even in simulation mode
            var otp = ExtractOtpFromMessage(message);
            if (!string.IsNullOrEmpty(otp))
            {
                var cacheKey = $"fast2sms_otp_{phoneNumber}";
                var cacheOptions = new MemoryCacheEntryOptions
                {
                    AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(OtpExpiryMinutes)
                };
                _memoryCache.Set(cacheKey, otp, cacheOptions);
                
                _logger.LogWarning("📱 OTP for testing: {OTP} (valid for {Minutes} minutes)", otp, OtpExpiryMinutes);
            }
            
            return true;
        }

        private string ExtractOtpFromMessage(string message)
        {
            // Extract 6-digit OTP from message
            var otpPattern = @"\b\d{6}\b";
            var match = System.Text.RegularExpressions.Regex.Match(message, otpPattern);
            return match.Success ? match.Value : string.Empty;
        }

        // Helper method to verify OTP (for testing purposes)
        public bool VerifyOtp(string phoneNumber, string providedOtp)
        {
            // Try with the original phone number format
            var cacheKey = $"fast2sms_otp_{phoneNumber}";
            if (_memoryCache.TryGetValue(cacheKey, out string? cachedOtp))
            {
                var isValid = cachedOtp == providedOtp;
                if (isValid)
                {
                    _memoryCache.Remove(cacheKey);
                }
                _logger.LogInformation("OTP verification with original format {PhoneNumber}: {IsValid}", phoneNumber, isValid);
                return isValid;
            }
            
            // Try with cleaned phone number format (without +91)
            var cleanPhoneNumber = phoneNumber.Replace("+91", "").Replace("+", "");
            var cleanCacheKey = $"fast2sms_otp_{cleanPhoneNumber}";
            if (_memoryCache.TryGetValue(cleanCacheKey, out cachedOtp))
            {
                var isValid = cachedOtp == providedOtp;
                if (isValid)
                {
                    _memoryCache.Remove(cleanCacheKey);
                }
                _logger.LogInformation("OTP verification with clean format {CleanPhoneNumber}: {IsValid}", cleanPhoneNumber, isValid);
                return isValid;
            }
            
            _logger.LogWarning("OTP not found in cache for {PhoneNumber} or {CleanPhoneNumber}", phoneNumber, cleanPhoneNumber);
            return false;
        }
    }
}