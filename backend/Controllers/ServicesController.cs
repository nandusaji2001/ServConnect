using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using ServConnect.Models;
using ServConnect.Services;

namespace ServConnect.Controllers
{
    // Handles service catalog features: predefined, custom, linking providers, and listing providers by service
    public class ServicesController : Controller
    {
        private readonly IServiceCatalog _catalog;
        private readonly UserManager<Users> _userManager;

        public ServicesController(IServiceCatalog catalog, UserManager<Users> userManager)
        {
            _catalog = catalog;
            _userManager = userManager;
        }

        // User-facing: page to browse all services
        [AllowAnonymous]
        public IActionResult Browse()
        {
            return View();
        }

        // API: all services (predefined + custom discovered via provider links)
        [HttpGet("/api/services/all")]
        [AllowAnonymous]
        public async Task<IActionResult> GetAllServiceNames()
        {
            var names = await _catalog.GetAllAvailableServiceNamesAsync();
            return Ok(names);
        }

        // API: providers who offer a selected service
        [HttpGet("/api/services/{slug}/providers")]
        [AllowAnonymous]
        public async Task<IActionResult> ProvidersByService(string slug)
        {
            var providers = await _catalog.GetProviderLinksBySlugAsync(slug);
            return Ok(providers);
        }

        // Provider: manage own service links UI
        [Authorize(Roles = RoleTypes.ServiceProvider)]
        public IActionResult Manage()
        {
            ViewBag.Predefined = _catalog.GetPredefined();
            return View();
        }

        // API: link current provider to a service
        [HttpPost("/api/services/link")]
        [Authorize(Roles = RoleTypes.ServiceProvider)]
        public async Task<IActionResult> Link([FromBody] LinkRequest req)
        {
            if (string.IsNullOrWhiteSpace(req.ServiceName)) return BadRequest("Service name required");
            var me = await _userManager.GetUserAsync(User);
            if (me == null) return Unauthorized();
            var link = await _catalog.LinkProviderAsync(me.Id, req.ServiceName);
            return Ok(link);
        }

        // API: list my linked services
        [HttpGet("/api/services/mine")]
        [Authorize(Roles = RoleTypes.ServiceProvider)]
        public async Task<IActionResult> Mine()
        {
            var me = await _userManager.GetUserAsync(User);
            if (me == null) return Unauthorized();
            var links = await _catalog.GetProviderLinksByProviderAsync(me.Id);
            return Ok(links);
        }

        // API: unlink (deactivate) a service from my profile
        [HttpDelete("/api/services/mine/{id}")]
        [Authorize(Roles = RoleTypes.ServiceProvider)]
        public async Task<IActionResult> Unlink(string id)
        {
            var me = await _userManager.GetUserAsync(User);
            if (me == null) return Unauthorized();
            var ok = await _catalog.UnlinkAsync(id, me.Id);
            return ok ? NoContent() : NotFound();
        }

        public class LinkRequest
        {
            public string ServiceName { get; set; } = string.Empty;
        }
    }
}