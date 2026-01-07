using Microsoft.Extensions.Caching.Memory;
using System.Text;
using System.Text.Json;

namespace ServConnect.Services
{
    public class Msg91SmsService : ISmsService
    {
        private readonly HttpClient _httpClient;
        private readonly IConfiguration _configuration;
        private readonly ILogger<Msg91SmsService> _logger;
        private readonly IMemoryCache _memoryCache;
        private const int OtpExpiryMinutes = 10;
        private const string Msg91BaseUrl = "https://control.msg91.com/api/v5";

        public Msg91SmsService(
            HttpClient httpClient,
            IConfiguration configuration,
            ILogger<Msg91SmsService> logger,
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
                var authKey = _configuration["Msg91:AuthKey"];
                var senderId = _configuration["Msg91:SenderId"] ?? "SRVCON";
                var simulate = _configuration.GetValue<bool>("Msg91:SimulateInDevelopment", false);

                if (string.IsNullOrEmpty(authKey))
                {
                    _logger.LogError("MSG91 AuthKey not configured");
                    return false;
                }

                if (simulate)
                {
                    _logger.LogWarning("MSG91 SIMULATION - Message to {PhoneNumber}: {Message}", phoneNumber, message);
                    return true;
                }

                // Clean phone number - ensure it has country code
                var cleanPhoneNumber = CleanPhoneNumber(phoneNumber);

                _logger.LogInformation("Sending SMS via MSG91 to {PhoneNumber}", cleanPhoneNumber);

                // MSG91 Flow API for sending SMS
                var flowId = _configuration["Msg91:FlowId"];
                
                if (!string.IsNullOrEmpty(flowId))
                {
                    return await SendViaFlowApi(authKey, cleanPhoneNumber, message, flowId);
                }
                else
                {
                    return await SendViaSendSmsApi(authKey, cleanPhoneNumber, message, senderId);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "MSG91 provider failed for {PhoneNumber}", phoneNumber);
                return false;
            }
        }

        private async Task<bool> SendViaFlowApi(string authKey, string phoneNumber, string message, string flowId)
        {
            try
            {
                var url = $"{Msg91BaseUrl}/flow/";
                var senderId = _configuration["Msg91:SenderId"] ?? "SRVCON";

                // Parse the SOS message to extract variables
                // Expected format: "SOS ALERT! {name} needs help. Call: {phone}. Blood: {blood}. Time: {time}. Respond NOW! -ServConnect"
                var variables = ParseSosMessage(message);

                // MSG91 Flow API requires this specific structure
                var requestBody = new Dictionary<string, object>
                {
                    { "flow_id", flowId },
                    { "sender", senderId },
                    { "mobiles", phoneNumber },
                    { "name", variables.GetValueOrDefault("name", "Elder") },
                    { "phone", variables.GetValueOrDefault("phone", "") },
                    { "blood", variables.GetValueOrDefault("blood", "") },
                    { "time", variables.GetValueOrDefault("time", DateTime.Now.ToString("dd-MMM hh:mm tt")) }
                };

                var jsonContent = JsonSerializer.Serialize(requestBody);
                _logger.LogInformation("MSG91 Flow API Request: {Request}", jsonContent);

                using var request = new HttpRequestMessage(HttpMethod.Post, url);
                request.Headers.Add("authkey", authKey);
                request.Content = new StringContent(jsonContent, Encoding.UTF8, "application/json");

                var response = await _httpClient.SendAsync(request);
                var responseContent = await response.Content.ReadAsStringAsync();

                _logger.LogInformation("MSG91 Flow API Response: {Response}", responseContent);

                // Check for success - must contain "success" and NOT contain "error"
                if (responseContent.Contains("\"type\":\"success\"") && !responseContent.Contains("\"type\":\"error\""))
                {
                    _logger.LogInformation("MSG91 sent successfully to {PhoneNumber}", phoneNumber);
                    CacheOtpIfPresent(phoneNumber, message);
                    return true;
                }

                _logger.LogWarning("MSG91 Flow API failed: {Response}", responseContent);
                return false;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "MSG91 Flow API exception");
                return false;
            }
        }

        private static Dictionary<string, string> ParseSosMessage(string message)
        {
            var variables = new Dictionary<string, string>();

            // Extract name: "SOS ALERT! {name} needs help"
            var nameMatch = System.Text.RegularExpressions.Regex.Match(message, @"SOS ALERT!\s*(.+?)\s*needs help");
            if (nameMatch.Success)
                variables["name"] = nameMatch.Groups[1].Value.Trim();

            // Extract phone: "Call: {phone}"
            var phoneMatch = System.Text.RegularExpressions.Regex.Match(message, @"Call:\s*([+\d]+)");
            if (phoneMatch.Success)
                variables["phone"] = phoneMatch.Groups[1].Value.Trim();

            // Extract blood: "Blood: {blood}"
            var bloodMatch = System.Text.RegularExpressions.Regex.Match(message, @"Blood:\s*([A-Za-z+-]+)");
            if (bloodMatch.Success)
                variables["blood"] = bloodMatch.Groups[1].Value.Trim();

            // Extract time: "Time: {time}"
            var timeMatch = System.Text.RegularExpressions.Regex.Match(message, @"Time:\s*(.+?)\.");
            if (timeMatch.Success)
                variables["time"] = timeMatch.Groups[1].Value.Trim();

            return variables;
        }

        private async Task<bool> SendViaSendSmsApi(string authKey, string phoneNumber, string message, string senderId)
        {
            try
            {
                // Use MSG91's Send SMS API (allows custom messages without template)
                var mobileNumber = phoneNumber.StartsWith("91") ? phoneNumber.Substring(2) : phoneNumber;
                if (mobileNumber.Length > 10) mobileNumber = mobileNumber.Substring(mobileNumber.Length - 10);

                // MSG91 Send SMS API endpoint (legacy but works for custom messages)
                var url = $"https://api.msg91.com/api/sendhttp.php?" +
                          $"authkey={authKey}&" +
                          $"mobiles={mobileNumber}&" +
                          $"message={Uri.EscapeDataString(message)}&" +
                          $"sender={senderId}&" +
                          $"route=4&" +
                          $"country=91";

                _logger.LogInformation("MSG91 SendHTTP Request to mobile: {Phone}", mobileNumber);

                using var request = new HttpRequestMessage(HttpMethod.Get, url);
                var response = await _httpClient.SendAsync(request);
                var responseContent = await response.Content.ReadAsStringAsync();

                _logger.LogInformation("MSG91 SendHTTP Response: {Response}", responseContent);

                // MSG91 sendhttp returns request ID on success (alphanumeric string)
                // Error responses contain "error" or specific error codes
                if (response.IsSuccessStatusCode &&
                    !responseContent.Contains("error", StringComparison.OrdinalIgnoreCase) &&
                    !responseContent.Contains("invalid", StringComparison.OrdinalIgnoreCase))
                {
                    _logger.LogInformation("MSG91 sent successfully to {PhoneNumber}", phoneNumber);
                    CacheOtpIfPresent(phoneNumber, message);
                    return true;
                }

                _logger.LogWarning("MSG91 SendHTTP failed: {Response}", responseContent);
                return false;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "MSG91 SendSMS exception");
                return false;
            }
        }

        public async Task<bool> SendOtpAsync(string phoneNumber, string otp)
        {
            try
            {
                var authKey = _configuration["Msg91:AuthKey"];
                var templateId = _configuration["Msg91:OtpTemplateId"];
                var simulate = _configuration.GetValue<bool>("Msg91:SimulateInDevelopment", false);
                var cleanPhoneNumber = CleanPhoneNumber(phoneNumber);

                if (simulate)
                {
                    _logger.LogWarning("MSG91 OTP SIMULATION - OTP {Otp} to {PhoneNumber}", otp, cleanPhoneNumber);
                    CacheOtp(cleanPhoneNumber, otp);
                    return true;
                }

                if (string.IsNullOrEmpty(authKey))
                {
                    _logger.LogError("MSG91 AuthKey not configured");
                    return false;
                }

                // Use MSG91 OTP API if template is configured
                if (!string.IsNullOrEmpty(templateId))
                {
                    return await SendOtpViaOtpApi(authKey, cleanPhoneNumber, otp, templateId);
                }

                // Fallback to regular SMS
                var message = $"Your ServConnect OTP is {otp}. Valid for {OtpExpiryMinutes} minutes.";
                return await SendSmsAsync(phoneNumber, message);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "MSG91 OTP failed for {PhoneNumber}", phoneNumber);
                return false;
            }
        }

        private async Task<bool> SendOtpViaOtpApi(string authKey, string phoneNumber, string otp, string templateId)
        {
            try
            {
                var url = $"{Msg91BaseUrl}/otp";

                var requestBody = new
                {
                    template_id = templateId,
                    mobile = phoneNumber,
                    otp = otp
                };

                var jsonContent = JsonSerializer.Serialize(requestBody);
                
                using var request = new HttpRequestMessage(HttpMethod.Post, url);
                request.Headers.Add("authkey", authKey);
                request.Content = new StringContent(jsonContent, Encoding.UTF8, "application/json");

                var response = await _httpClient.SendAsync(request);
                var responseContent = await response.Content.ReadAsStringAsync();

                _logger.LogInformation("MSG91 OTP API Response: {Response}", responseContent);

                // Check for success - must contain "success" and NOT contain "error"
                if (responseContent.Contains("\"type\":\"success\"") && !responseContent.Contains("\"type\":\"error\""))
                {
                    CacheOtp(phoneNumber, otp);
                    return true;
                }

                _logger.LogWarning("MSG91 OTP API failed: {Response}", responseContent);
                return false;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "MSG91 OTP API exception");
                return false;
            }
        }

        private string CleanPhoneNumber(string phoneNumber)
        {
            // Remove spaces and special characters
            var cleaned = phoneNumber.Replace(" ", "").Replace("-", "").Replace("+", "").Trim();
            
            // Ensure it starts with country code 91 for India
            if (!cleaned.StartsWith("91") && cleaned.Length == 10)
            {
                cleaned = "91" + cleaned;
            }
            
            return cleaned;
        }

        private void CacheOtpIfPresent(string phoneNumber, string message)
        {
            var otp = ExtractOtpFromMessage(message);
            if (!string.IsNullOrEmpty(otp))
            {
                CacheOtp(phoneNumber, otp);
            }
        }

        private void CacheOtp(string phoneNumber, string otp)
        {
            var cacheKey = $"msg91_otp_{phoneNumber}";
            var cacheOptions = new MemoryCacheEntryOptions
            {
                AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(OtpExpiryMinutes)
            };
            _memoryCache.Set(cacheKey, otp, cacheOptions);
        }

        private static string ExtractOtpFromMessage(string message)
        {
            var otpPattern = @"\b\d{6}\b";
            var match = System.Text.RegularExpressions.Regex.Match(message, otpPattern);
            return match.Success ? match.Value : string.Empty;
        }

        public bool VerifyOtp(string phoneNumber, string providedOtp)
        {
            var cleanPhoneNumber = CleanPhoneNumber(phoneNumber);
            var cacheKey = $"msg91_otp_{cleanPhoneNumber}";
            
            if (_memoryCache.TryGetValue(cacheKey, out string? cachedOtp))
            {
                var isValid = cachedOtp == providedOtp;
                if (isValid) _memoryCache.Remove(cacheKey);
                return isValid;
            }

            return false;
        }
    }
}
