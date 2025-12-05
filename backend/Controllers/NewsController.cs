using Microsoft.AspNetCore.Mvc;
using ServConnect.Services;

namespace ServConnect.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class NewsController : ControllerBase
    {
        private readonly INewsService _newsService;

        public NewsController(INewsService newsService)
        {
            _newsService = newsService;
        }

        [HttpGet]
        public async Task<IActionResult> GetNews([FromQuery] string location = "India")
        {
            if (string.IsNullOrWhiteSpace(location))
            {
                location = "India";
            }

            var news = await _newsService.GetNewsByLocationAsync(location);
            return Ok(news);
        }
    }
}
