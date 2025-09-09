using AspNetCore.Identity.MongoDbCore.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Http;
using ServConnect.Models;
using ServConnect.Services;
using System.Drawing;

namespace ServConnect.Controllers
{
    [Authorize(Roles = RoleTypes.Admin)]
    public class AdminController : Controller
    {
        private readonly UserManager<Users> _userManager;
        private readonly RoleManager<MongoIdentityRole> _roleManager;
        private readonly IItemService _itemService;
        private readonly IAdvertisementService _adService;
        private readonly IWebHostEnvironment _env;

        public AdminController(
            UserManager<Users> userManager,
            RoleManager<MongoIdentityRole> roleManager,
            IItemService itemService,
            IAdvertisementService adService,
            IWebHostEnvironment env)
        {
            _userManager = userManager;
            _roleManager = roleManager;
            _itemService = itemService;
            _adService = adService;
            _env = env;
        }

        public async Task<IActionResult> Dashboard()
        {
            // Get current admin user
            var currentUser = await _userManager.GetUserAsync(User);
            ViewBag.AdminName = currentUser?.FullName ?? "Admin";

            // Get user statistics
            var allUsers = _userManager.Users.ToList();
            var serviceProviders = await _userManager.GetUsersInRoleAsync(RoleTypes.ServiceProvider);
            var vendors = await _userManager.GetUsersInRoleAsync(RoleTypes.Vendor);
            var regularUsers = await _userManager.GetUsersInRoleAsync(RoleTypes.User);

            ViewBag.TotalUsers = allUsers.Count;
            ViewBag.ServiceProviders = serviceProviders.Count;
            ViewBag.Vendors = vendors.Count;
            ViewBag.RegularUsers = regularUsers.Count;

            return View();
        }

        public async Task<IActionResult> Users()
        {
            var users = _userManager.Users.ToList();
            return View(users);
        }

        // Admin: view user details, including roles and items if vendor/provider
        [HttpGet]
        public async Task<IActionResult> UserDetails(Guid id)
        {
            var user = await _userManager.FindByIdAsync(id.ToString());
            if (user == null) return NotFound();
            var roles = await _userManager.GetRolesAsync(user);
            var items = await _itemService.GetByOwnerAsync(user.Id);

            var vm = new ViewModels.AdminUserDetailsViewModel
            {
                User = user,
                Roles = roles.ToList(),
                Items = items
            };
            return View(vm);
        }

        // Admin: view all users as JSON (optional API endpoint)
        [HttpGet]
        [Route("api/admin/users")] 
        public IActionResult GetAllUsers()
        {
            var users = _userManager.Users.Select(u => new {
                u.Id, u.FullName, u.Email, u.PhoneNumber
            }).ToList();
            return new JsonResult(users);
        }

        // Admin: delete a user by id
        [HttpDelete]
        [Route("api/admin/users/{id}")]
        public async Task<IActionResult> DeleteUser(Guid id)
        {
            var user = await _userManager.FindByIdAsync(id.ToString());
            if (user == null) return NotFound();
            var result = await _userManager.DeleteAsync(user);
            if (!result.Succeeded) return StatusCode(500, string.Join("; ", result.Errors.Select(e => e.Description)));
            return NoContent();
        }

        public async Task<IActionResult> Analytics()
        {
            // Placeholder for analytics
            return View();
        }

        public async Task<IActionResult> Settings()
        {
            // Placeholder for system settings
            return View();
        }

        // ================= Advertisement Management =================
        [HttpGet]
        public async Task<IActionResult> Advertisements()
        {
            var ads = await _adService.GetAllAsync();
            return View(ads);
        }

        [HttpGet]
        public IActionResult CreateAd()
        {
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CreateAd(IFormFile image, string? targetUrl)
        {
            if (image == null || image.Length == 0)
            {
                ModelState.AddModelError("image", "Please upload an image.");
                return View();
            }

            // Process: crop/resize any image to 728x90
            const int targetW = 728;
            const int targetH = 90;
            try
            {
                using var stream = image.OpenReadStream();
                using var src = System.Drawing.Image.FromStream(stream);

                var targetRatio = (double)targetW / targetH;
                var srcRatio = (double)src.Width / src.Height;

                // Compute crop rectangle to match target ratio
                System.Drawing.Rectangle cropRect;
                if (srcRatio > targetRatio)
                {
                    // Too wide: crop width
                    var cropW = (int)Math.Round(src.Height * targetRatio);
                    var x = (src.Width - cropW) / 2;
                    cropRect = new System.Drawing.Rectangle(x, 0, cropW, src.Height);
                }
                else
                {
                    // Too tall: crop height
                    var cropH = (int)Math.Round(src.Width / targetRatio);
                    var y = (src.Height - cropH) / 2;
                    cropRect = new System.Drawing.Rectangle(0, y, src.Width, cropH);
                }

                using var dest = new System.Drawing.Bitmap(targetW, targetH);
                using (var g = System.Drawing.Graphics.FromImage(dest))
                {
                    g.CompositingQuality = System.Drawing.Drawing2D.CompositingQuality.HighQuality;
                    g.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;
                    g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.HighQuality;
                    g.PixelOffsetMode = System.Drawing.Drawing2D.PixelOffsetMode.HighQuality;
                    g.DrawImage(src, new System.Drawing.Rectangle(0, 0, targetW, targetH), cropRect, System.Drawing.GraphicsUnit.Pixel);
                }

                // Save to wwwroot/ads as JPEG
                var adsFolder = Path.Combine(_env.WebRootPath, "ads");
                if (!Directory.Exists(adsFolder)) Directory.CreateDirectory(adsFolder);
                var fileName = $"ad_{Guid.NewGuid()}.jpg";
                var savePath = Path.Combine(adsFolder, fileName);
                dest.Save(savePath, System.Drawing.Imaging.ImageFormat.Jpeg);

                var ad = new Advertisement
                {
                    ImageUrl = $"/ads/{fileName}",
                    TargetUrl = string.IsNullOrWhiteSpace(targetUrl) ? null : targetUrl,
                    IsActive = true,
                    CreatedAt = DateTime.UtcNow
                };
                await _adService.CreateAsync(ad);
            }
            catch
            {
                ModelState.AddModelError("image", "Invalid image file.");
                return View();
            }

            TempData["AdMessage"] = "Advertisement uploaded successfully.";
            return RedirectToAction(nameof(Advertisements));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteAd(string id)
        {
            if (string.IsNullOrWhiteSpace(id)) return RedirectToAction(nameof(Advertisements));
            // Try to delete file as well
            var ads = await _adService.GetAllAsync();
            var ad = ads.FirstOrDefault(a => a.Id == id);
            if (ad != null)
            {
                try
                {
                    var physical = Path.Combine(_env.WebRootPath, ad.ImageUrl.TrimStart('/').Replace('/', Path.DirectorySeparatorChar));
                    if (System.IO.File.Exists(physical)) System.IO.File.Delete(physical);
                }
                catch { /* ignore */ }
            }
            await _adService.DeleteAsync(id);
            TempData["AdMessage"] = "Advertisement deleted.";
            return RedirectToAction(nameof(Advertisements));
        }
    }
}