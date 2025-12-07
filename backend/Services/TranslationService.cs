using System.Text.Json;
using System.Text.Json.Serialization;
using System.Net;

namespace ServConnect.Services
{
    public class TranslationService : ITranslationService
    {
        private readonly HttpClient _httpClient;
        private readonly ILogger<TranslationService> _logger;
        private readonly IConfiguration _configuration;

        public TranslationService(HttpClient httpClient, ILogger<TranslationService> logger, IConfiguration configuration)
        {
            _httpClient = httpClient;
            _logger = logger;
            _configuration = configuration;
        }

        public async Task<string> TranslateTextAsync(string text, string sourceLanguage, string targetLanguage)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(text))
                {
                    return text;
                }

                // Use MyMemory Translation API instead of LibreTranslate
                _logger.LogInformation($"Translating text from {sourceLanguage} to {targetLanguage}: {text.Substring(0, Math.Min(50, text.Length))}...");

                // Construct the MyMemory API URL
                var encodedText = WebUtility.UrlEncode(text);
                var langPair = $"{sourceLanguage}|{targetLanguage}";
                var url = $"https://api.mymemory.translated.net/get?q={encodedText}&langpair={langPair}";

                _httpClient.Timeout = TimeSpan.FromSeconds(30);
                
                using (var cts = new System.Threading.CancellationTokenSource(TimeSpan.FromSeconds(30)))
                {
                    try
                    {
                        var response = await _httpClient.GetAsync(url, cts.Token);

                        if (!response.IsSuccessStatusCode)
                        {
                            var errorContent = await response.Content.ReadAsStringAsync();
                            _logger.LogWarning($"Translation API failed with status {response.StatusCode}: {errorContent}");
                            return text;
                        }

                        var responseContent = await response.Content.ReadAsStringAsync();
                        _logger.LogInformation($"Translation response: {responseContent}");
                        
                        var translationResponse = JsonSerializer.Deserialize<MyMemoryTranslationResponse>(
                            responseContent,
                            new JsonSerializerOptions { PropertyNameCaseInsensitive = true }
                        );

                        // Extract translated text from MyMemory response
                        var result = translationResponse?.ResponseData?.TranslatedText ?? text;
                        _logger.LogInformation($"Translated result: {result.Substring(0, Math.Min(50, result.Length))}...");
                        
                        return result;
                    }
                    catch (System.Threading.Tasks.TaskCanceledException ex)
                    {
                        _logger.LogError($"Translation API request timed out: {ex.Message}");
                        return text;
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error during translation: {ex.Message}, Stack: {ex.StackTrace}");
                return text;
            }
        }

        // MyMemory API response classes
        private class MyMemoryTranslationResponse
        {
            [JsonPropertyName("responseData")]
            public ResponseData? ResponseData { get; set; }
            
            [JsonPropertyName("responseStatus")]
            public int ResponseStatus { get; set; }
        }

        private class ResponseData
        {
            [JsonPropertyName("translatedText")]
            public string? TranslatedText { get; set; }
        }
    }
}