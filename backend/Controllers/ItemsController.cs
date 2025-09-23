using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Http;
using ServConnect.Models;
using ServConnect.Services;

namespace ServConnect.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class ItemsController : ControllerBase
    {
        private readonly IItemService _itemService;
        private readonly UserManager<Users> _userManager;

        public ItemsController(IItemService itemService, UserManager<Users> userManager)
        {
            _itemService = itemService;
            _userManager = userManager;
        }

        // Users can view all active items
        [HttpGet]
        [AllowAnonymous]
        public async Task<IActionResult> GetAll()
        {
            var items = await _itemService.GetAllAsync();
            return Ok(items);
        }

        // Vendors: list own items
        [HttpGet("mine")]
        [Authorize(Roles = RoleTypes.Vendor)]
        [ServiceFilter(typeof(ServConnect.Filters.RequireApprovedUserFilter))]
        public async Task<IActionResult> GetMine()
        {
            var me = await _userManager.GetUserAsync(User);
            if (me == null) return Unauthorized();
            var items = await _itemService.GetByOwnerAsync(me.Id);
            return Ok(items);
        }

        // Vendors: create item (service providers should use ProviderServices via /api/services/link)
        [HttpPost]
        [Authorize(Roles = RoleTypes.Vendor)]
        [ServiceFilter(typeof(ServConnect.Filters.RequireApprovedUserFilter))]
        public async Task<IActionResult> Create([FromBody] Item input)
        {
            var me = await _userManager.GetUserAsync(User);
            if (me == null) return Unauthorized();

            if (string.IsNullOrWhiteSpace(input.Title))
                return BadRequest("Title is required");
            if (input.Price < 0)
                return BadRequest("Price must be greater than or equal to 0");

            input.Id = null!; // let Mongo assign
            input.OwnerId = me.Id;
            var roles = await _userManager.GetRolesAsync(me);
            input.OwnerRole = RoleTypes.Vendor;
            input.CreatedAt = DateTime.UtcNow;
            input.UpdatedAt = DateTime.UtcNow;
            // Respect requested activation state (default to true if unspecified)
            input.IsActive = input.IsActive;

            var created = await _itemService.CreateAsync(input);
            return CreatedAtAction(nameof(GetById), new { id = created.Id }, created);
        }

        [HttpGet("{id}")]
        [AllowAnonymous]
        public async Task<IActionResult> GetById(string id)
        {
            var item = await _itemService.GetByIdAsync(id);
            if (item == null) return NotFound();
            return Ok(item);
        }

        // Vendors: update own item
        [HttpPut("{id}")]
        [Authorize(Roles = RoleTypes.Vendor)]
        [ServiceFilter(typeof(ServConnect.Filters.RequireApprovedUserFilter))]
        public async Task<IActionResult> Update(string id, [FromBody] Item input)
        {
            var me = await _userManager.GetUserAsync(User);
            if (me == null) return Unauthorized();

            var existing = await _itemService.GetByIdAsync(id);
            if (existing == null) return NotFound();
            if (existing.OwnerId != me.Id) return Forbid();

            existing.Title = input.Title;
            existing.Description = input.Description;
            existing.Price = input.Price;
            existing.IsActive = input.IsActive;
            // allow updating category too
            existing.Category = input.Category;

            var ok = await _itemService.UpdateAsync(existing);
            return ok ? NoContent() : StatusCode(500, "Update failed");
        }

        // Vendors: delete own item
        [HttpDelete("{id}")]
        [Authorize(Roles = RoleTypes.Vendor)]
        [ServiceFilter(typeof(ServConnect.Filters.RequireApprovedUserFilter))]
        public async Task<IActionResult> DeleteMine(string id)
        {
            var me = await _userManager.GetUserAsync(User);
            if (me == null) return Unauthorized();

            var existing = await _itemService.GetByIdAsync(id);
            if (existing == null) return NotFound();
            if (existing.OwnerId != me.Id) return Forbid();

            var ok = await _itemService.DeleteAsync(id);
            return ok ? NoContent() : StatusCode(500, "Delete failed");
        }

        // Admin: delete any item
        [HttpDelete("admin/{id}")]
        [Authorize(Roles = RoleTypes.Admin)]
        public async Task<IActionResult> AdminDelete(string id)
        {
            var ok = await _itemService.DeleteAsync(id);
            return ok ? NoContent() : NotFound();
        }

        // Admin: list all items (incl. inactive)
        [HttpGet("admin/all")]
        [Authorize(Roles = RoleTypes.Admin)]
        public async Task<IActionResult> AdminAll()
        {
            var items = await _itemService.GetAllAsync(includeInactive: true);
            return Ok(items);
        }

        // Providers/Vendors: create item with image upload (multipart/form-data)
        [HttpPost("with-image")]
        [Authorize(Roles = $"{RoleTypes.ServiceProvider},{RoleTypes.Vendor}")]
        [RequestSizeLimit(10_000_000)] // ~10 MB
        public async Task<IActionResult> CreateWithImage(
            [FromForm] string title,
            [FromForm] string? description,
            [FromForm] decimal price,
            [FromForm] string? category,
            [FromForm] string? sku,
            [FromForm] int stock,
            IFormFile? image)
        {
            var me = await _userManager.GetUserAsync(User);
            if (me == null) return Unauthorized();

            string? imageUrl = null;
            if (image != null && image.Length > 0)
            {
                var uploadsRoot = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "images", "items");
                Directory.CreateDirectory(uploadsRoot);
                var fileName = $"{Guid.NewGuid()}{Path.GetExtension(image.FileName)}";
                var fullPath = Path.Combine(uploadsRoot, fileName);
                using (var stream = System.IO.File.Create(fullPath))
                {
                    await image.CopyToAsync(stream);
                }
                imageUrl = $"/images/items/{fileName}";
            }

            var roles = await _userManager.GetRolesAsync(me);
            var ownerRole = roles.Contains(RoleTypes.Vendor) ? RoleTypes.Vendor : RoleTypes.ServiceProvider;

            var item = new Item
            {
                Id = null!,
                OwnerId = me.Id,
                OwnerRole = ownerRole,
                Title = title,
                Description = description,
                Price = price,
                Category = category,
                SKU = sku,
                Stock = stock,
                ImageUrl = imageUrl,
                IsActive = true,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            var created = await _itemService.CreateAsync(item);
            return CreatedAtAction(nameof(GetById), new { id = created.Id }, created);
        }
    }
}