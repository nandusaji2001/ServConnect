using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Diagnostics;
using ServConnect.Models;

namespace ServConnect.Controllers
{
    public class HomeController : Controller
    {
        private readonly ILogger<HomeController> _logger;

        public HomeController(ILogger<HomeController> logger)
        {
            _logger = logger;
        }

        public IActionResult Index()
        {
            return View();
        }

        // Role-aware dashboard: redirect regular users to hero home (Index)
        [Authorize]
        public IActionResult Dashboard()
        {
            if (User.IsInRole(RoleTypes.Admin)) return RedirectToAction("Dashboard", "Admin");
            if (User.IsInRole(RoleTypes.Vendor)) return RedirectToAction("Dashboard", "Vendor");
            if (User.IsInRole(RoleTypes.ServiceProvider)) return RedirectToAction("Dashboard", "ServiceProvider");
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

        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error()
        {
            return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
        }
    }
}
