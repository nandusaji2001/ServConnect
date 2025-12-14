using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Diagnostics;
using ServConnect.Models;
using ServConnect.Services;
using Microsoft.AspNetCore.Identity;

namespace ServConnect.Controllers
{
    public class HomeController : Controller
    {
        private readonly ILogger<HomeController> _logger;
        private readonly UserManager<Users> _userManager;
        private readonly IServiceCatalog _serviceCatalog;
        private readonly IBookingService _bookingService;
        private readonly IRatingService _ratingService;

        public HomeController(
            ILogger<HomeController> logger,
            UserManager<Users> userManager,
            IServiceCatalog serviceCatalog,
            IBookingService bookingService,
            IRatingService ratingService)
        {
            _logger = logger;
            _userManager = userManager;
            _serviceCatalog = serviceCatalog;
            _bookingService = bookingService;
            _ratingService = ratingService;
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
                // Get dynamic statistics
                var totalUsers = _userManager.Users.Count();
                var serviceProviders = _userManager.GetUsersInRoleAsync(RoleTypes.ServiceProvider).Result.Count;
                
                // Get all provider services to calculate completed services and average rating
                var allServices = await _serviceCatalog.GetAllAvailableServiceNamesAsync();
                var completedBookings = 0;
                var totalRating = 0.0m;
                var ratingCount = 0;

                // Get service providers and their bookings
                var providers = await _userManager.GetUsersInRoleAsync(RoleTypes.ServiceProvider);
                foreach (var provider in providers)
                {
                    var providerBookings = await _bookingService.GetForProviderAsync(provider.Id);
                    completedBookings += providerBookings.Count(b => b.IsCompleted);

                    // Calculate ratings for completed bookings
                    foreach (var booking in providerBookings.Where(b => b.IsCompleted && b.UserRating.HasValue))
                    {
                        totalRating += booking.UserRating.Value;
                        ratingCount++;
                    }
                }

                var averageRating = ratingCount > 0 ? totalRating / ratingCount : 4.8m;

                // Pass data to view
                ViewBag.TotalUsers = totalUsers;
                ViewBag.ServiceProviders = serviceProviders;
                ViewBag.CompletedServices = completedBookings;
                ViewBag.AverageRating = Math.Round(averageRating, 1);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching dashboard statistics");
                // Fallback to default values if there's an error
                ViewBag.TotalUsers = 0;
                ViewBag.ServiceProviders = 0;
                ViewBag.CompletedServices = 0;
                ViewBag.AverageRating = 4.8m;
            }

            return View();
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

        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error()
        {
            return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
        }
    }
}
