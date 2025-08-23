using AspNetCore.Identity.MongoDbCore.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using ServConnect.Models;

namespace ServConnect.Controllers
{
    [Authorize(Roles = RoleTypes.Admin)]
    public class AdminController : Controller
    {
        private readonly UserManager<Users> _userManager;
        private readonly RoleManager<MongoIdentityRole> _roleManager;

        public AdminController(UserManager<Users> userManager, RoleManager<MongoIdentityRole> roleManager)
        {
            _userManager = userManager;
            _roleManager = roleManager;
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
    }
}