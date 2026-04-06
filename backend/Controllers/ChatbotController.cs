using Microsoft.AspNetCore.Mvc;
using ServConnect.Services;

namespace ServConnect.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class ChatbotController : ControllerBase
    {
        private readonly IChatbotService _chatbotService;

        public ChatbotController(IChatbotService chatbotService)
        {
            _chatbotService = chatbotService;
        }

        [HttpPost("query")]
        public IActionResult Query([FromBody] ChatbotQueryRequest request)
        {
            if (string.IsNullOrWhiteSpace(request.Message))
            {
                return BadRequest(new { error = "Message is required" });
            }

            var response = _chatbotService.GetResponse(request.Message);
            return Ok(response);
        }

        [HttpGet("suggestions")]
        public IActionResult GetSuggestions()
        {
            var suggestions = _chatbotService.GetQuickSuggestions();
            return Ok(suggestions);
        }
    }

    public class ChatbotQueryRequest
    {
        public string Message { get; set; } = string.Empty;
    }
}
