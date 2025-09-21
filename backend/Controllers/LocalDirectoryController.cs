using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using ServConnect.Models;
using ServConnect.Services;
using System.Linq;
using System.Text.RegularExpressions;

namespace ServConnect.Controllers
{
    // Admin CRUD for local services and categories + public discovery endpoints
    public class LocalDirectoryController : Controller
    {
        private readonly ILocalDirectory _directory;
        public LocalDirectoryController(ILocalDirectory directory)
        {
            _directory = directory;
        }

        // Admin UI: list + create
        [Authorize(Roles = RoleTypes.Admin)]
        public async Task<IActionResult> Manage()
        {
            ViewBag.Categories = await _directory.GetCategoriesAsync();
            return View();
        }

        // Public UI: discover page
        [AllowAnonymous]
        public IActionResult Discover()
        {
            return View();
        }

        // Public UI: browse results page
        [HttpGet("/local/services/browse")]
        [AllowAnonymous]
        public IActionResult Browse([FromQuery] string? q, [FromQuery] string? categorySlug, [FromQuery] string? locationName)
        {
            ViewBag.Q = q;
            ViewBag.CategorySlug = categorySlug;
            ViewBag.LocationName = locationName;
            return View();
        }

        // API: categories list (public)
        [HttpGet("/api/local/categories")]
        [AllowAnonymous]
        public async Task<IActionResult> Categories()
        {
            var cats = await _directory.GetCategoriesAsync();
            return Ok(cats);
        }

        // API: admin creates or ensures category
        [HttpPost("/api/local/categories")] 
        [Authorize(Roles = RoleTypes.Admin)]
        public async Task<IActionResult> CreateCategory([FromBody] CategoryReq req)
        {
            if (string.IsNullOrWhiteSpace(req.Name)) return BadRequest("Category name required");
            var cat = await _directory.EnsureCategoryAsync(req.Name);
            return Ok(cat);
        }

        // API: admin create service
        [HttpPost("/api/local/services")] 
        [Authorize(Roles = RoleTypes.Admin)]
        public async Task<IActionResult> CreateService([FromBody] ServiceReq req)
        {
            if (string.IsNullOrWhiteSpace(req.Name) || string.IsNullOrWhiteSpace(req.CategoryName))
                return BadRequest("Name and Category are required");
            var svc = new LocalService
            {
                Name = req.Name.Trim(),
                CategoryName = req.CategoryName.Trim(),
                MapUrl = string.IsNullOrWhiteSpace(req.MapUrl) ? null : req.MapUrl.Trim(),
                Address = req.Address,
                Phone = req.Phone,
                IsActive = true
            };
            var created = await _directory.CreateServiceAsync(svc);
            return Ok(created);
        }

        // API: admin update service
        [HttpPut("/api/local/services/{id}")] 
        [Authorize(Roles = RoleTypes.Admin)]
        public async Task<IActionResult> UpdateService(string id, [FromBody] ServiceReq req)
        {
            var existing = await _directory.GetServiceAsync(id);
            if (existing == null) return NotFound();
            existing.Name = req.Name?.Trim() ?? existing.Name;
            existing.CategoryName = req.CategoryName?.Trim() ?? existing.CategoryName;
            existing.MapUrl = string.IsNullOrWhiteSpace(req.MapUrl) ? existing.MapUrl : req.MapUrl.Trim();
            existing.Address = req.Address;
            existing.Phone = req.Phone;
            existing.IsActive = req.IsActive ?? existing.IsActive;
            var ok = await _directory.UpdateServiceAsync(existing);
            return ok ? NoContent() : StatusCode(500);
        }

        // API: admin delete service
        [HttpDelete("/api/local/services/{id}")] 
        [Authorize(Roles = RoleTypes.Admin)]
        public async Task<IActionResult> DeleteService(string id)
        {
            var ok = await _directory.DeleteServiceAsync(id);
            return ok ? NoContent() : NotFound();
        }

        // Public discovery: list by category
        [HttpGet("/api/local/services")] 
        [AllowAnonymous]
        public async Task<IActionResult> Discover([FromQuery] string? q, [FromQuery] string? categorySlug, [FromQuery] string? locationName)
        {
            var list = await _directory.SearchAsync(q, categorySlug, locationName);

            // Enrich with user rating averages and sort desc by rating
            var ratingSvc = HttpContext.RequestServices.GetRequiredService<IRatingService>();
            // Build stable keys for services (OSM/mongo)
            var keyPairs = list.Select(s => (s, ratingSvc.DetectKeyFrom(s.Id, s.MapUrl))).ToList();
            var averages = await ratingSvc.GetAveragesAsync(keyPairs.Select(k => k.Item2));
            foreach (var p in keyPairs)
            {
                if (averages.TryGetValue(p.Item2, out var agg))
                {
                    p.s.Rating = agg.average;
                    p.s.RatingCount = agg.count;
                }
            }
            list = list.OrderByDescending(x => x.Rating).ThenBy(x => x.Name).ToList();
            return Ok(list);
        }

        // Public: redirect to the stored Google Maps URL
        [HttpGet("/local/services/{id}/map")]
        [AllowAnonymous]
        public async Task<IActionResult> RedirectToMap(string id)
        {
            var svc = await _directory.GetServiceAsync(id);
            if (svc == null) return NotFound();
            if (string.IsNullOrWhiteSpace(svc.MapUrl)) return BadRequest("No map url set for this service");
            return Redirect(svc.MapUrl);
        }

        public class CategoryReq { public string Name { get; set; } = string.Empty; }
        public class ServiceReq 
        { 
            public string Name { get; set; } = string.Empty; 
            public string CategoryName { get; set; } = string.Empty; 
            public string? MapUrl { get; set; }
            public string? Address { get; set; }
            public string? Phone { get; set; }
            public bool? IsActive { get; set; }
        }

        // API: submit rating for a service (logged-in users)
        [HttpPost("/api/local/services/{id}/rating")]
        [Authorize]
        public async Task<IActionResult> SubmitRating(string id, [FromBody] RatingReq req)
        {
            if (req == null || req.Rating < 1 || req.Rating > 5) return BadRequest("Rating must be 1-5");
            var ratingSvc = HttpContext.RequestServices.GetRequiredService<IRatingService>();
            var svc = await _directory.GetServiceAsync(id); // if admin-created
            string key = svc != null
                ? ratingSvc.ComposeKey("mongo", svc.Id!)
                : ratingSvc.DetectKeyFrom(id, null);

            var userId = User?.Identity?.Name ?? User?.FindFirst("sub")?.Value ?? User?.FindFirst("userid")?.Value ?? "unknown";
            await ratingSvc.SubmitAsync(userId, key, req.Rating);
            return NoContent();
        }
        public class RatingReq { public int Rating { get; set; } }
    }
}