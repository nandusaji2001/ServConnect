using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using ServConnect.Models;
using ServConnect.Services;
using ServConnect.ViewModels;

namespace ServConnect.Controllers
{
    // Handles service catalog features: predefined, custom, linking providers, and listing providers by service
    public class ServicesController : Controller
    {
        private readonly IServiceCatalog _catalog;
        private readonly UserManager<Users> _userManager;
        private readonly IBookingService _bookingService;

        public ServicesController(IServiceCatalog catalog, UserManager<Users> userManager, IBookingService bookingService)
        {
            _catalog = catalog;
            _userManager = userManager;
            _bookingService = bookingService;
        }

        // User-facing: page to browse all services
        [AllowAnonymous]
        public async Task<IActionResult> Browse()
        {
            if (User.Identity?.IsAuthenticated == true)
            {
                var me = await _userManager.GetUserAsync(User);
                if (me != null)
                {
                    var bookings = await _bookingService.GetForUserAsync(me.Id);
                    ViewBag.ActiveBookingsCount = bookings.Count(b => b.Status == BookingStatus.Accepted && !b.IsCompleted);
                }
                else
                {
                    ViewBag.ActiveBookingsCount = 0;
                }
            }
            else
            {
                ViewBag.ActiveBookingsCount = 0;
            }
            return View();
        }

        // User-facing: page to view providers for a selected service (by slug)
        [HttpGet("/services/providers/{slug}")]
        [AllowAnonymous]
        public async Task<IActionResult> Providers(string slug)
        {
            var providers = await _catalog.GetProviderLinksBySlugAsync(slug);
            var def = await _catalog.GetBySlugAsync(slug);
            var vm = new ServiceProvidersViewModel
            {
                ServiceName = def?.Name ?? slug.Replace('-', ' '),
                ServiceSlug = slug,
                Providers = providers
            };
            return View(vm);
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
        [ServiceFilter(typeof(ServConnect.Filters.RequireApprovedUserFilter))]
        public IActionResult Manage()
        {
            ViewBag.Predefined = _catalog.GetPredefined();
            return View();
        }

        // API: link current provider to a service
        [HttpPost("/api/services/link")]
        [Authorize(Roles = RoleTypes.ServiceProvider)]
        [ServiceFilter(typeof(ServConnect.Filters.RequireApprovedUserFilter))]
        public async Task<IActionResult> Link([FromBody] LinkRequest req)
        {
            if (string.IsNullOrWhiteSpace(req.ServiceName)) return BadRequest("Service name required");
            if (req.Price <= 0) return BadRequest("Price must be greater than 0");
            var me = await _userManager.GetUserAsync(User);
            if (me == null) return Unauthorized();
            var link = await _catalog.LinkProviderAsync(me.Id, req.ServiceName, req.Description, req.Price, req.PriceUnit, req.Currency, req.AvailableDays, req.AvailableHours);
            return Ok(link);
        }

        // API: list my linked services
        [HttpGet("/api/services/mine")]
        [Authorize(Roles = RoleTypes.ServiceProvider)]
        [ServiceFilter(typeof(ServConnect.Filters.RequireApprovedUserFilter))]
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
        [ServiceFilter(typeof(ServConnect.Filters.RequireApprovedUserFilter))]
        public async Task<IActionResult> Unlink(string id)
        {
            var me = await _userManager.GetUserAsync(User);
            if (me == null) return Unauthorized();
            var ok = await _catalog.UnlinkAsync(id, me.Id);
            return ok ? NoContent() : NotFound();
        }

        // API: relink (activate) a previously unlinked service
        [HttpPost("/api/services/mine/{id}/relink")]
        [Authorize(Roles = RoleTypes.ServiceProvider)]
        [ServiceFilter(typeof(ServConnect.Filters.RequireApprovedUserFilter))]
        public async Task<IActionResult> Relink(string id)
        {
            var me = await _userManager.GetUserAsync(User);
            if (me == null) return Unauthorized();
            var ok = await _catalog.RelinkAsync(id, me.Id);
            return ok ? NoContent() : NotFound();
        }

        // API: update an existing link (edit)
        public class UpdateLinkRequest
        {
            public string Description { get; set; } = string.Empty;
            public decimal Price { get; set; } = 0;
            public string PriceUnit { get; set; } = "per service";
            public string Currency { get; set; } = "USD";
            public List<string> AvailableDays { get; set; } = new();
            public string AvailableHours { get; set; } = "9:00 AM - 6:00 PM";
        }

        [HttpPut("/api/services/mine/{id}")]
        [Authorize(Roles = RoleTypes.ServiceProvider)]
        [ServiceFilter(typeof(ServConnect.Filters.RequireApprovedUserFilter))]
        public async Task<IActionResult> UpdateLink(string id, [FromBody] UpdateLinkRequest req)
        {
            var me = await _userManager.GetUserAsync(User);
            if (me == null) return Unauthorized();
            var ok = await _catalog.UpdateLinkAsync(id, me.Id, req.Description, req.Price, req.PriceUnit, req.Currency, req.AvailableDays, req.AvailableHours);
            return ok ? NoContent() : NotFound();
        }

        // API: delete a link permanently
        [HttpDelete("/api/services/mine/{id}/hard")]
        [Authorize(Roles = RoleTypes.ServiceProvider)]
        [ServiceFilter(typeof(ServConnect.Filters.RequireApprovedUserFilter))]
        public async Task<IActionResult> DeleteLinkHard(string id)
        {
            var me = await _userManager.GetUserAsync(User);
            if (me == null) return Unauthorized();
            var ok = await _catalog.DeleteLinkAsync(id, me.Id);
            return ok ? NoContent() : NotFound();
        }

        public class LinkRequest
        {
            public string ServiceName { get; set; } = string.Empty;
            public string Description { get; set; } = string.Empty;
            public decimal Price { get; set; } = 0;
            public string PriceUnit { get; set; } = "per service";
            public string Currency { get; set; } = "USD";
            public List<string> AvailableDays { get; set; } = new();
            public string AvailableHours { get; set; } = "9:00 AM - 6:00 PM";
        }
    }
}