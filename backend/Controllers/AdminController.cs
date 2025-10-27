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

        private readonly IComplaintService _complaintService;

        private readonly IAdvertisementRequestService _adReqService;

        private readonly IServiceCatalog _serviceCatalog;
        private readonly IRevenueService _revenueService;

        public AdminController(
            UserManager<Users> userManager,
            RoleManager<MongoIdentityRole> roleManager,
            IItemService itemService,
            IAdvertisementService adService,
            IWebHostEnvironment env,
            IComplaintService complaintService,
            IAdvertisementRequestService adReqService,
            IServiceCatalog serviceCatalog,
            IRevenueService revenueService)
        {
            _userManager = userManager;
            _roleManager = roleManager;
            _itemService = itemService;
            _adService = adService;
            _env = env;
            _complaintService = complaintService;
            _adReqService = adReqService;
            _serviceCatalog = serviceCatalog;
            _revenueService = revenueService;
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

            // New complaints count for quick stat
            try
            {
                var allComplaints = await _complaintService.GetAllAsync(status: ServConnect.Models.ComplaintStatus.New);
                ViewBag.NewComplaints = allComplaints.Count;
            }
            catch { ViewBag.NewComplaints = 0; }

            // Pending user verification count for notification
            try
            {
                var pendingUsers = allUsers.Where(u => u.IsProfileCompleted && !u.IsAdminApproved).ToList();
                ViewBag.PendingUsersCount = pendingUsers.Count;
            }
            catch { ViewBag.PendingUsersCount = 0; }

            // Revenue analytics
            try
            {
                // Sync revenue data from all sources
                await _revenueService.SyncRevenueFromPaymentsAsync();
                await _revenueService.SyncRevenueFromAdvertisementsAsync();
                await _revenueService.SyncRevenueFromBookingPaymentsAsync();

                var totalRevenue = await _revenueService.GetTotalRevenueAsync();
                var monthlyRevenue = await _revenueService.GetMonthlyRevenueAsync(6);
                var revenueBreakdown = await _revenueService.GetRevenueBreakdownAsync();
                var growthRate = await _revenueService.GetRevenueGrowthRateAsync(3);

                ViewBag.TotalRevenue = totalRevenue;
                ViewBag.MonthlyRevenue = monthlyRevenue;
                ViewBag.RevenueBreakdown = revenueBreakdown;
                ViewBag.RevenueGrowthRate = growthRate;
                ViewBag.ServicePublicationRevenue = revenueBreakdown.GetValueOrDefault(RevenueType.ServicePublication, 0);
                ViewBag.AdvertisementRevenue = revenueBreakdown.GetValueOrDefault(RevenueType.AdvertisementPayment, 0);
                ViewBag.BookingCommissionRevenue = revenueBreakdown.GetValueOrDefault(RevenueType.BookingCommission, 0);
            }
            catch 
            { 
                ViewBag.TotalRevenue = 0m;
                ViewBag.MonthlyRevenue = new Dictionary<string, decimal>();
                ViewBag.RevenueBreakdown = new Dictionary<RevenueType, decimal>();
                ViewBag.RevenueGrowthRate = 0.0;
                ViewBag.ServicePublicationRevenue = 0m;
                ViewBag.AdvertisementRevenue = 0m;
                ViewBag.BookingCommissionRevenue = 0m;
            }

            return View();
        }

        // Lightweight API for dashboard cards
        [HttpGet]
        [Route("api/complaints")]
        public async Task<IActionResult> GetComplaints([FromQuery] int? limit = null)
        {
            var list = await _complaintService.GetAllAsync();
            if (limit.HasValue && limit.Value > 0)
            {
                list = list.Take(limit.Value).ToList();
            }

            var shaped = list.Select(c => new
            {
                id = c.Id,
                category = c.Category,
                complainantName = c.ComplainantName,
                complainantRole = c.ComplainantRole,
                createdAt = c.CreatedAt,
                status = c.Status,
                description = c.Description
            }).ToList();
            return new JsonResult(shaped);
        }

        // Full complaints/messages page for admin
        [HttpGet]
        public async Task<IActionResult> Complaints(string? status, string? role, string? category)
        {
            var list = await _complaintService.GetAllAsync(status, role, category);
            return View(list);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UpdateComplaintStatus(string id, string status, string? adminNote)
        {
            if (string.IsNullOrWhiteSpace(id) || string.IsNullOrWhiteSpace(status))
            {
                return RedirectToAction(nameof(Complaints));
            }
            await _complaintService.UpdateStatusAsync(id, status, adminNote);
            return RedirectToAction(nameof(Complaints));
        }

        public async Task<IActionResult> Users()
        {
            var users = _userManager.Users.ToList();
            return View(users);
        }

        // New: Verify Users Card - show users needing admin review with full details
        [HttpGet]
        public async Task<IActionResult> VerifyUsers()
        {
            var users = _userManager.Users.ToList();
            // Candidates for verification: profile completed but not yet admin approved
            var toVerify = users.Where(u => u.IsProfileCompleted && !u.IsAdminApproved).ToList();
            return View(toVerify);
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

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ApproveUser(Guid id)
        {
            var user = await _userManager.FindByIdAsync(id.ToString());
            if (user == null) return NotFound();
            user.IsAdminApproved = true;
            user.AdminReviewedAtUtc = DateTime.UtcNow;
            await _userManager.UpdateAsync(user);
            TempData["UserMessage"] = "User approved.";
            return RedirectToAction(nameof(VerifyUsers));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> RejectUser(Guid id, string? note)
        {
            var user = await _userManager.FindByIdAsync(id.ToString());
            if (user == null) return NotFound();
            user.IsAdminApproved = false;
            user.AdminReviewNote = string.IsNullOrWhiteSpace(note) ? "Rejected" : note;
            user.AdminReviewedAtUtc = DateTime.UtcNow;
            await _userManager.UpdateAsync(user);
            TempData["UserMessage"] = "User rejected.";
            return RedirectToAction(nameof(VerifyUsers));
        }

        // Admin: view all users as JSON (optional API endpoint)
        [HttpGet]
        [Route("api/admin/users")] 
        public async Task<IActionResult> GetAllUsers()
        {
            var list = _userManager.Users.ToList();
            var shaped = new List<object>(list.Count);
            foreach (var u in list)
            {
                var roles = await _userManager.GetRolesAsync(u);
                var isAdmin = roles.Contains(RoleTypes.Admin);
                shaped.Add(new {
                    u.Id,
                    u.FullName,
                    u.Email,
                    u.PhoneNumber,
                    Suspended = u.LockoutEnd.HasValue && u.LockoutEnd.Value > DateTimeOffset.UtcNow,
                    IsAdmin = isAdmin
                });
            }
            return new JsonResult(shaped);
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

        // Admin: suspend/unsuspend a user
        [HttpPost]
        [Route("api/admin/users/{id}/suspend")]
        public async Task<IActionResult> SuspendUser(Guid id, [FromQuery] bool suspend = true)
        {
            var user = await _userManager.FindByIdAsync(id.ToString());
            if (user == null) return NotFound();

            // Ensure lockout is enabled and set lockout end accordingly
            if (!await _userManager.GetLockoutEnabledAsync(user))
            {
                await _userManager.SetLockoutEnabledAsync(user, true);
            }

            var end = suspend ? DateTimeOffset.MaxValue : (DateTimeOffset?)null;
            var res = await _userManager.SetLockoutEndDateAsync(user, end);
            if (!res.Succeeded)
            {
                return StatusCode(500, string.Join("; ", res.Errors.Select(e => e.Description)));
            }
            return Ok(new { id = user.Id, suspended = suspend });
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

        // ================= Service Management =================
        [HttpGet]
        public async Task<IActionResult> Services()
        {
            // Get all provider services from all providers
            var allProviders = await _userManager.GetUsersInRoleAsync(RoleTypes.ServiceProvider);
            var allServices = new List<ProviderService>();
            
            foreach (var provider in allProviders)
            {
                var providerServices = await _serviceCatalog.GetProviderLinksByProviderAsync(provider.Id);
                allServices.AddRange(providerServices);
            }
            
            return View(allServices);
        }

        // API: Get all services as JSON
        [HttpGet]
        [Route("api/admin/services")]
        public async Task<IActionResult> GetAllServices()
        {
            var allProviders = await _userManager.GetUsersInRoleAsync(RoleTypes.ServiceProvider);
            var allServices = new List<object>();
            
            foreach (var provider in allProviders)
            {
                var providerServices = await _serviceCatalog.GetProviderLinksByProviderAsync(provider.Id);
                foreach (var service in providerServices)
                {
                    allServices.Add(new
                    {
                        id = service.Id,
                        serviceName = service.ServiceName,
                        description = service.Description,
                        providerName = service.ProviderName,
                        providerEmail = service.ProviderEmail,
                        price = service.Price,
                        currency = service.Currency,
                        priceUnit = service.PriceUnit,
                        isActive = service.IsActive,
                        isAvailable = service.IsAvailable,
                        rating = service.Rating,
                        reviewCount = service.ReviewCount,
                        availableHours = service.AvailableHours,
                        availableDays = service.AvailableDays,
                        createdAt = service.CreatedAt
                    });
                }
            }
            
            return Ok(allServices);
        }

        // API: Delete a service
        [HttpDelete]
        [Route("api/admin/services/{id}")]
        public async Task<IActionResult> DeleteService(string id)
        {
            if (string.IsNullOrWhiteSpace(id)) return BadRequest("Service ID is required");
            
            // Find the service and its provider
            var allProviders = await _userManager.GetUsersInRoleAsync(RoleTypes.ServiceProvider);
            ProviderService? targetService = null;
            Guid providerId = Guid.Empty;
            
            foreach (var provider in allProviders)
            {
                var providerServices = await _serviceCatalog.GetProviderLinksByProviderAsync(provider.Id);
                targetService = providerServices.FirstOrDefault(s => s.Id == id);
                if (targetService != null)
                {
                    providerId = provider.Id;
                    break;
                }
            }
            
            if (targetService == null) return NotFound("Service not found");
            
            var success = await _serviceCatalog.DeleteLinkAsync(id, providerId);
            return success ? NoContent() : StatusCode(500, "Failed to delete service");
        }

        // API: Suspend/Unsuspend a service
        [HttpPost]
        [Route("api/admin/services/{id}/suspend")]
        public async Task<IActionResult> SuspendService(string id, [FromQuery] bool suspend = true)
        {
            if (string.IsNullOrWhiteSpace(id)) return BadRequest("Service ID is required");
            
            // Find the service and its provider
            var allProviders = await _userManager.GetUsersInRoleAsync(RoleTypes.ServiceProvider);
            ProviderService? targetService = null;
            Guid providerId = Guid.Empty;
            
            foreach (var provider in allProviders)
            {
                var providerServices = await _serviceCatalog.GetProviderLinksByProviderAsync(provider.Id);
                targetService = providerServices.FirstOrDefault(s => s.Id == id);
                if (targetService != null)
                {
                    providerId = provider.Id;
                    break;
                }
            }
            
            if (targetService == null) return NotFound("Service not found");
            
            // Toggle the service availability (suspend/unsuspend)
            bool success;
            if (suspend)
            {
                success = await _serviceCatalog.UnlinkAsync(id, providerId);
            }
            else
            {
                success = await _serviceCatalog.RelinkAsync(id, providerId);
            }
            
            return success ? Ok(new { id, suspended = suspend }) : StatusCode(500, "Failed to update service status");
        }

        // API: Update/Edit a service
        [HttpPut]
        [Route("api/admin/services/{id}")]
        public async Task<IActionResult> UpdateService(string id, [FromBody] UpdateServiceRequest request)
        {
            if (string.IsNullOrWhiteSpace(id)) return BadRequest("Service ID is required");
            
            // Find the service and its provider
            var allProviders = await _userManager.GetUsersInRoleAsync(RoleTypes.ServiceProvider);
            ProviderService? targetService = null;
            Guid providerId = Guid.Empty;
            
            foreach (var provider in allProviders)
            {
                var providerServices = await _serviceCatalog.GetProviderLinksByProviderAsync(provider.Id);
                targetService = providerServices.FirstOrDefault(s => s.Id == id);
                if (targetService != null)
                {
                    providerId = provider.Id;
                    break;
                }
            }
            
            if (targetService == null) return NotFound("Service not found");
            
            var success = await _serviceCatalog.UpdateLinkAsync(
                id, 
                providerId, 
                request.Description, 
                request.Price, 
                request.PriceUnit, 
                request.Currency, 
                request.AvailableDays, 
                request.AvailableHours
            );
            
            return success ? Ok() : StatusCode(500, "Failed to update service");
        }

        public class UpdateServiceRequest
        {
            public string Description { get; set; } = string.Empty;
            public decimal Price { get; set; } = 0;
            public string PriceUnit { get; set; } = "per service";
            public string Currency { get; set; } = "USD";
            public List<string> AvailableDays { get; set; } = new();
            public string AvailableHours { get; set; } = "9:00 AM - 6:00 PM";
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
        [System.Runtime.Versioning.SupportedOSPlatform("windows")]
        public async Task<IActionResult> CreateAd(IFormFile image, string? targetUrl, AdvertisementType adType = AdvertisementType.BottomPage, 
            double? cropX = null, double? cropY = null, double? cropWidth = null, double? cropHeight = null)
        {
            if (image == null || image.Length == 0)
            {
                ModelState.AddModelError("image", "Please upload an image.");
                return View();
            }

            // Set dimensions based on advertisement type
            int targetW, targetH;
            if (adType == AdvertisementType.HeroBanner)
            {
                targetW = 600;
                targetH = 180;
            }
            else
            {
                targetW = 728;
                targetH = 90;
            }
            try
            {
                using var stream = image.OpenReadStream();
                using var src = System.Drawing.Image.FromStream(stream);

                System.Drawing.Rectangle cropRect;
                
                // Use user's crop selection if provided, otherwise auto-crop from center
                if (cropX.HasValue && cropY.HasValue && cropWidth.HasValue && cropHeight.HasValue)
                {
                    // Use user's crop selection
                    cropRect = new System.Drawing.Rectangle(
                        (int)Math.Round(cropX.Value),
                        (int)Math.Round(cropY.Value),
                        (int)Math.Round(cropWidth.Value),
                        (int)Math.Round(cropHeight.Value)
                    );
                    
                    // Ensure crop rectangle is within image bounds
                    cropRect.X = Math.Max(0, Math.Min(cropRect.X, src.Width - 1));
                    cropRect.Y = Math.Max(0, Math.Min(cropRect.Y, src.Height - 1));
                    cropRect.Width = Math.Max(1, Math.Min(cropRect.Width, src.Width - cropRect.X));
                    cropRect.Height = Math.Max(1, Math.Min(cropRect.Height, src.Height - cropRect.Y));
                }
                else
                {
                    // Fallback to automatic center cropping
                    var targetRatio = (double)targetW / targetH;
                    var srcRatio = (double)src.Width / src.Height;

                    if (srcRatio > targetRatio)
                    {
                        var cropW = (int)Math.Round(src.Height * targetRatio);
                        var x = (src.Width - cropW) / 2;
                        cropRect = new System.Drawing.Rectangle(x, 0, cropW, src.Height);
                    }
                    else
                    {
                        var cropH = (int)Math.Round(src.Width / targetRatio);
                        var y = (src.Height - cropH) / 2;
                        cropRect = new System.Drawing.Rectangle(0, y, src.Width, cropH);
                    }
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
                    Type = adType,
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



        [HttpGet]
        public async Task<IActionResult> AdvertisementRequests()
        {
            var list = await _adReqService.GetAllAsync();
            return View(list);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ApproveAdRequest(string id)
        {
            if (string.IsNullOrWhiteSpace(id)) return RedirectToAction(nameof(AdvertisementRequests));
            var req = await _adReqService.GetByIdAsync(id);
            if (req == null) return RedirectToAction(nameof(AdvertisementRequests));
            if (!req.IsPaid)
            {
                TempData["AdReqMessage"] = "Cannot approve unpaid request.";
                return RedirectToAction(nameof(AdvertisementRequests));
            }

            // On approval, convert to active advertisement
            var ad = new Advertisement
            {
                ImageUrl = req.ImageUrl,
                TargetUrl = req.TargetUrl,
                Type = req.Type,
                IsActive = true,
                CreatedAt = DateTime.UtcNow
            };
            await _adService.CreateAsync(ad);
            await _adReqService.ApproveAndSetExpiryAsync(id);
            TempData["AdReqMessage"] = "Request approved and published as advertisement.";
            return RedirectToAction(nameof(AdvertisementRequests));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> RejectAdRequest(string id)
        {
            if (string.IsNullOrWhiteSpace(id)) return RedirectToAction(nameof(AdvertisementRequests));
            await _adReqService.UpdateStatusAsync(id, AdRequestStatus.Rejected);
            TempData["AdReqMessage"] = "Request rejected.";
            return RedirectToAction(nameof(AdvertisementRequests));
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

        // Revenue Analytics API Endpoints
        [HttpGet]
        [Route("api/admin/revenue/analytics")]
        public async Task<IActionResult> GetRevenueAnalytics([FromQuery] DateTime? fromDate = null, [FromQuery] DateTime? toDate = null)
        {
            try
            {
                var analytics = await _revenueService.GetRevenueAnalyticsAsync(fromDate, toDate);
                return Json(analytics);
            }
            catch (Exception ex)
            {
                return BadRequest(new { error = ex.Message });
            }
        }

        [HttpGet]
        [Route("api/admin/revenue/predictions")]
        public async Task<IActionResult> GetRevenuePredictions([FromQuery] string periods = "1,3,6,9,12")
        {
            try
            {
                var monthsList = periods.Split(',').Select(int.Parse).ToList();
                var predictions = await _revenueService.PredictRevenueAsync(monthsList);
                return Json(predictions);
            }
            catch (Exception ex)
            {
                return BadRequest(new { error = ex.Message });
            }
        }

        [HttpGet]
        [Route("api/admin/revenue/monthly")]
        public async Task<IActionResult> GetMonthlyRevenue([FromQuery] int months = 12)
        {
            try
            {
                var monthlyRevenue = await _revenueService.GetMonthlyRevenueAsync(months);
                return Json(monthlyRevenue);
            }
            catch (Exception ex)
            {
                return BadRequest(new { error = ex.Message });
            }
        }

        [HttpGet]
        [Route("api/admin/revenue/breakdown")]
        public async Task<IActionResult> GetRevenueBreakdown([FromQuery] DateTime? fromDate = null, [FromQuery] DateTime? toDate = null)
        {
            try
            {
                var breakdown = await _revenueService.GetRevenueBreakdownAsync(fromDate, toDate);
                return Json(breakdown);
            }
            catch (Exception ex)
            {
                return BadRequest(new { error = ex.Message });
            }
        }

        [HttpPost]
        [Route("api/admin/revenue/sync")]
        public async Task<IActionResult> SyncRevenueData()
        {
            try
            {
                await _revenueService.SyncRevenueFromPaymentsAsync();
                await _revenueService.SyncRevenueFromAdvertisementsAsync();
                await _revenueService.SyncRevenueFromBookingPaymentsAsync();
                
                return Json(new { success = true, message = "Revenue data synchronized successfully" });
            }
            catch (Exception ex)
            {
                return BadRequest(new { error = ex.Message });
            }
        }

        // Revenue Analytics Page
        public async Task<IActionResult> Revenue()
        {
            try
            {
                // Sync revenue data
                await _revenueService.SyncRevenueFromPaymentsAsync();
                await _revenueService.SyncRevenueFromAdvertisementsAsync();
                await _revenueService.SyncRevenueFromBookingPaymentsAsync();

                var analytics = await _revenueService.GetRevenueAnalyticsAsync();
                var predictions = await _revenueService.PredictRevenueAsync(new List<int> { 1, 3, 6, 9, 12 });
                var detailedBreakdown = await _revenueService.GetDetailedRevenueBreakdownAsync();
                
                ViewBag.Analytics = analytics;
                ViewBag.Predictions = predictions;
                ViewBag.DetailedBreakdown = detailedBreakdown;
                ViewBag.PaidServicePayments = await _revenueService.GetPaidServicePaymentsAsync();
                ViewBag.PaidAdvertisements = await _revenueService.GetPaidAdvertisementsAsync();
                
                return View();
            }
            catch (Exception ex)
            {
                ViewBag.Error = ex.Message;
                return View();
            }
        }

    }
}