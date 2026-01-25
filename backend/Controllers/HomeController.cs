using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Diagnostics;
using ServConnect.Models;
using ServConnect.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Caching.Memory;

namespace ServConnect.Controllers
{
    public class HomeController : Controller
    {
        private readonly ILogger<HomeController> _logger;
        private readonly UserManager<Users> _userManager;
        private readonly IServiceCatalog _serviceCatalog;
        private readonly IBookingService _bookingService;
        private readonly IRatingService _ratingService;
        private readonly IMemoryCache _cache;

        public HomeController(
            ILogger<HomeController> logger,
            UserManager<Users> userManager,
            IServiceCatalog serviceCatalog,
            IBookingService bookingService,
            IRatingService ratingService,
            IMemoryCache cache)
        {
            _logger = logger;
            _userManager = userManager;
            _serviceCatalog = serviceCatalog;
            _bookingService = bookingService;
            _ratingService = ratingService;
            _cache = cache;
        }

        public async Task<IActionResult> Index()
        {
            // Check if user is an elder - redirect to Elder Dashboard
            if (User.Identity?.IsAuthenticated == true)
            {
                var currentUser = await _userManager.GetUserAsync(User);
                if (currentUser != null && currentUser.IsElder)
                {
                    return RedirectToAction("Dashboard", "ElderCare");
                }
            }

            try
            {
                // Use cached statistics (refresh every 5 minutes) to avoid slow N+1 queries
                var stats = await _cache.GetOrCreateAsync("HomePageStats", async entry =>
                {
                    entry.AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(5);
                    
                    var totalUsers = _userManager.Users.Count();
                    var providers = await _userManager.GetUsersInRoleAsync(RoleTypes.ServiceProvider);
                    var serviceProviders = providers.Count;
                    
                    // Calculate stats in parallel for better performance
                    var completedBookings = 0;
                    var totalRating = 0.0m;
                    var ratingCount = 0;

                    // Batch fetch bookings for all providers at once if possible
                    // For now, limit to first 20 providers to avoid timeout
                    var limitedProviders = providers.Take(20);
                    var bookingTasks = limitedProviders.Select(p => _bookingService.GetForProviderAsync(p.Id));
                    var allBookings = await Task.WhenAll(bookingTasks);

                    foreach (var providerBookings in allBookings)
                    {
                        completedBookings += providerBookings.Count(b => b.IsCompleted);
                        foreach (var booking in providerBookings.Where(b => b.IsCompleted && b.UserRating.HasValue))
                        {
                            totalRating += booking.UserRating!.Value;
                            ratingCount++;
                        }
                    }

                    var averageRating = ratingCount > 0 ? totalRating / ratingCount : 4.8m;

                    return new HomePageStats
                    {
                        TotalUsers = totalUsers,
                        ServiceProviders = serviceProviders,
                        CompletedServices = completedBookings,
                        AverageRating = Math.Round(averageRating, 1)
                    };
                });

                ViewBag.TotalUsers = stats!.TotalUsers;
                ViewBag.ServiceProviders = stats.ServiceProviders;
                ViewBag.CompletedServices = stats.CompletedServices;
                ViewBag.AverageRating = stats.AverageRating;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching dashboard statistics");
                ViewBag.TotalUsers = 0;
                ViewBag.ServiceProviders = 0;
                ViewBag.CompletedServices = 0;
                ViewBag.AverageRating = 4.8m;
            }

            return View();
        }

        private class HomePageStats
        {
            public int TotalUsers { get; set; }
            public int ServiceProviders { get; set; }
            public int CompletedServices { get; set; }
            public decimal AverageRating { get; set; }
        }

        // Role-aware dashboard: redirect regular users to hero home (Index)
        [Authorize]
        public async Task<IActionResult> Dashboard()
        {
            if (User.IsInRole(RoleTypes.Admin)) return RedirectToAction("Dashboard", "Admin");
            if (User.IsInRole(RoleTypes.Vendor)) return RedirectToAction("Dashboard", "Vendor");
            if (User.IsInRole(RoleTypes.ServiceProvider)) return RedirectToAction("Dashboard", "ServiceProvider");
            
            // Check if user is an elder - redirect to Elder Dashboard
            var currentUser = await _userManager.GetUserAsync(User);
            if (currentUser != null && currentUser.IsElder)
            {
                return RedirectToAction("Dashboard", "ElderCare");
            }
            
            // For normal users, show the hero/landing with actions
            return RedirectToAction("Index", "Home");
        }

        [Authorize]
        public IActionResult Privacy()
        {
            return View();
        }

        // Public browse page for all active items (consumes /api/items)
        [AllowAnonymous]
        public IActionResult Items()
        {
            return View();
        }

        // Product details page
        [AllowAnonymous]
        public IActionResult ProductDetails(string id)
        {
            ViewBag.ProductId = id;
            return View();
        }

        // Shopping cart page
        [AllowAnonymous]
        public IActionResult Cart()
        {
            return View();
        }

        // User: My Orders dashboard
        [Authorize]
        public IActionResult MyOrders()
        {
            return View();
        }

        [AllowAnonymous]
        public IActionResult News(string location = "India")
        {
            ViewBag.Location = location;
            ViewBag.NewsLocations = new[] { "Kattappana", "Nedumkandam", "Kanjirappally", "Kumily", "Kuttikkanam" };
            return View();
        }

        [AllowAnonymous]
        public IActionResult About()
        {
            return View();
        }

        [AllowAnonymous]
        public IActionResult Contact()
        {
            return View();
        }

        [AllowAnonymous]
        public IActionResult ExploreServices()
        {
            return View();
        }

        /// <summary>
        /// Daily Essentials selection page - Milk, Newspaper, Gas subscriptions
        /// </summary>
        [Authorize]
        public IActionResult DailyEssentials()
        {
            return View();
        }

        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error()
        {
            return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
        }
    }
}
