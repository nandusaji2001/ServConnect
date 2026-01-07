using Microsoft.Extensions.Caching.Memory;
using System.Text.Json;
using System.Web;

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
                var simulate = _configuration.GetValue<bool>("Fast2SMS:SimulateInDevelopment", false);

                if (string.IsNullOrEmpty(apiKey))
                {
                    _logger.LogError("Fast2SMS API key not configured");
                    return false;
                }

                if (simulate)
                {
                    _logger.LogWarning("FAST2SMS SIMULATION - Message to {PhoneNumber}: {Message}", phoneNumber, message);
                    return true;
                }

                // Clean phone number for Indian format
                var cleanPhoneNumber = phoneNumber.Replace("+91", "").Replace("+", "").Replace(" ", "").Trim();

                _logger.LogInformation("Sending SMS via Fast2SMS to {PhoneNumber}", cleanPhoneNumber);

                // Try multiple approaches
                if (await TrySendViaGetApi(apiKey, cleanPhoneNumber, message))
                    return true;

                _logger.LogWarning("Fast2SMS failed for {PhoneNumber}", cleanPhoneNumber);
                return false;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Fast2SMS provider failed for {PhoneNumber}", phoneNumber);
                return false;
            }
        }

        private async Task<bool> TrySendViaGetApi(string apiKey, string phoneNumber, string message)
        {
            try
            {
                // Encode message for URL
                var encodedMessage = HttpUtility.UrlEncode(message);
                
                // Fast2SMS GET API - this is the most reliable method
                var url = $"https://www.fast2sms.com/dev/bulkV2?" +
                          $"authorization={apiKey}&" +
                          $"route=q&" +
                          $"message={encodedMessage}&" +
                          $"language=english&" +
                          $"flash=0&" +
                          $"numbers={phoneNumber}";

                _logger.LogInformation("Fast2SMS GET Request URL (without key): route=q&numbers={Phone}", phoneNumber);

                _httpClient.DefaultRequestHeaders.Clear();
                var response = await _httpClient.GetAsync(url);
                var responseContent = await response.Content.ReadAsStringAsync();

                _logger.LogInformation("Fast2SMS GET Response: {Response}", responseContent);

                // Check for success
                if (responseContent.Contains("\"return\":true"))
                {
                    _logger.LogInformation("Fast2SMS sent successfully to {PhoneNumber}", phoneNumber);
                    CacheOtpIfPresent(phoneNumber, message);
                    return true;
                }

                // If route q fails, try route p (promotional)
                _logger.LogWarning("Route q failed, trying route p");
                return await TrySendViaRouteP(apiKey, phoneNumber, message);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Fast2SMS GET API exception");
                return false;
            }
        }

        private async Task<bool> TrySendViaRouteP(string apiKey, string phoneNumber, string message)
        {
            try
            {
                var encodedMessage = HttpUtility.UrlEncode(message);
                
                var url = $"https://www.fast2sms.com/dev/bulkV2?" +
                          $"authorization={apiKey}&" +
                          $"route=p&" +
                          $"message={encodedMessage}&" +
                          $"language=english&" +
                          $"flash=0&" +
                          $"numbers={phoneNumber}";

                _httpClient.DefaultRequestHeaders.Clear();
                var response = await _httpClient.GetAsync(url);
                var responseContent = await response.Content.ReadAsStringAsync();

                _logger.LogInformation("Fast2SMS Route P Response: {Response}", responseContent);

                if (responseContent.Contains("\"return\":true"))
                {
                    _logger.LogInformation("Fast2SMS Route P sent successfully to {PhoneNumber}", phoneNumber);
                    CacheOtpIfPresent(phoneNumber, message);
                    return true;
                }

                _logger.LogWarning("Fast2SMS Route P also failed: {Response}", responseContent);
                return false;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Fast2SMS Route P exception");
                return false;
            }
        }

        private void CacheOtpIfPresent(string phoneNumber, string message)
        {
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
        }

        public async Task<bool> SendOtpAsync(string phoneNumber, string otp)
        {
            try
            {
                var apiKey = _configuration["Fast2SMS:ApiKey"];
                var simulate = _configuration.GetValue<bool>("Fast2SMS:SimulateInDevelopment", false);
                var cleanPhoneNumber = phoneNumber.Replace("+91", "").Replace("+", "").Replace(" ", "").Trim();

                if (simulate)
                {
                    _logger.LogWarning("FAST2SMS OTP SIMULATION - OTP {Otp} to {PhoneNumber}", otp, cleanPhoneNumber);
                    CacheOtpIfPresent(cleanPhoneNumber, otp);
                    return true;
                }

                // Use OTP route - this uses Fast2SMS default OTP template
                var url = $"https://www.fast2sms.com/dev/bulkV2?" +
                          $"authorization={apiKey}&" +
                          $"route=otp&" +
                          $"variables_values={otp}&" +
                          $"flash=0&" +
                          $"numbers={cleanPhoneNumber}";

                _httpClient.DefaultRequestHeaders.Clear();
                var response = await _httpClient.GetAsync(url);
                var responseContent = await response.Content.ReadAsStringAsync();

                _logger.LogInformation("Fast2SMS OTP Response: {Response}", responseContent);

                if (responseContent.Contains("\"return\":true"))
                {
                    CacheOtpIfPresent(cleanPhoneNumber, otp);
                    return true;
                }

                // Fallback to regular SMS
                var message = $"Your ServConnect OTP is {otp}. Valid for {OtpExpiryMinutes} minutes.";
                return await SendSmsAsync(phoneNumber, message);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Fast2SMS OTP failed for {PhoneNumber}", phoneNumber);
                return false;
            }
        }

        private static string ExtractOtpFromMessage(string message)
        {
            var otpPattern = @"\b\d{6}\b";
            var match = System.Text.RegularExpressions.Regex.Match(message, otpPattern);
            return match.Success ? match.Value : string.Empty;
        }

        public bool VerifyOtp(string phoneNumber, string providedOtp)
        {
            var cacheKey = $"fast2sms_otp_{phoneNumber}";
            if (_memoryCache.TryGetValue(cacheKey, out string? cachedOtp))
            {
                var isValid = cachedOtp == providedOtp;
                if (isValid) _memoryCache.Remove(cacheKey);
                return isValid;
            }

            var cleanPhoneNumber = phoneNumber.Replace("+91", "").Replace("+", "");
            var cleanCacheKey = $"fast2sms_otp_{cleanPhoneNumber}";
            if (_memoryCache.TryGetValue(cleanCacheKey, out cachedOtp))
            {
                var isValid = cachedOtp == providedOtp;
                if (isValid) _memoryCache.Remove(cleanCacheKey);
                return isValid;
            }

            return false;
        }
    }
}
