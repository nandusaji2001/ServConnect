using Microsoft.AspNetCore.Mvc;
using ServConnect.Services;

namespace ServConnect.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class NewsController : ControllerBase
    {
        private readonly INewsService _newsService;
        private readonly ITranslationService _translationService;

        public NewsController(INewsService newsService, ITranslationService translationService)
        {
            _newsService = newsService;
            _translationService = translationService;
        }

        [HttpGet]
        public async Task<IActionResult> GetNews([FromQuery] string location = "India", [FromQuery] string language = "en", [FromQuery] string country = "in")
        {
            if (string.IsNullOrWhiteSpace(location))
            {
                location = "India";
            }

            var supportedLanguages = new[] { "en", "hi", "ml", "ta", "te", "kn", "mr", "gu", "pa", "bn", "ur" };
            if (string.IsNullOrWhiteSpace(language) || !supportedLanguages.Contains(language.ToLower()))
            {
                language = "en";
            }

            if (string.IsNullOrWhiteSpace(country))
            {
                country = "in";
            }

            var news = await _newsService.GetNewsByLocationAsync(location, language.ToLower(), country.ToLower());
            return Ok(news);
        }

        [HttpPost("translate")]
        public async Task<IActionResult> TranslateText([FromBody] TranslationRequest request)
        {
            if (string.IsNullOrWhiteSpace(request.Text))
            {
                return BadRequest(new { error = "Text cannot be empty" });
            }

            var sourceLanguage = request.SourceLanguage ?? "en";
            var targetLanguage = request.TargetLanguage ?? "ml";

            try
            {
                var translatedText = await _translationService.TranslateTextAsync(
                    request.Text,
                    sourceLanguage,
                    targetLanguage
                );

                return Ok(new { translatedText = translatedText });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { error = ex.Message });
            }
        }

        [HttpGet("translate-test")]
        public async Task<IActionResult> TranslateTest([FromQuery] string text = "Hello world", [FromQuery] string target = "ml")
        {
            try
            {
                var result = await _translationService.TranslateTextAsync(text, "en", target);
                return Ok(new { 
                    original = text, 
                    translated = result,
                    success = result != text 
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { error = ex.Message, stackTrace = ex.StackTrace });
            }
        }
    }

    public class TranslationRequest
    {
        public string? Text { get; set; }
        public string? SourceLanguage { get; set; }
        public string? TargetLanguage { get; set; }
    }
}