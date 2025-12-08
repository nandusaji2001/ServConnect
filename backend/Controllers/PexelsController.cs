using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ServConnect.Services;

namespace ServConnect.Controllers
{
    [ApiController]
    [Route("api/pexels")]
    [AllowAnonymous]
    public class PexelsController : ControllerBase
    {
        private readonly IPexelsImageService _pexelsService;

        public PexelsController(IPexelsImageService pexelsService)
        {
            _pexelsService = pexelsService;
        }

        [HttpGet("image")]
        public async Task<IActionResult> GetImage([FromQuery] string serviceName)
        {
            if (string.IsNullOrWhiteSpace(serviceName))
                return BadRequest("serviceName is required");

            var result = await _pexelsService.GetImageForServiceAsync(serviceName);
            return Ok(result);
        }
    }
}
