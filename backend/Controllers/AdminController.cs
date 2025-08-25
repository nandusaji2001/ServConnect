using AspNetCore.Identity.MongoDbCore.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using ServConnect.Models;
using ServConnect.Services;

namespace ServConnect.Controllers
{
    [Authorize(Roles = RoleTypes.Admin)]
    public class AdminController : Controller
    {
        private readonly UserManager<Users> _userManager;
        private readonly RoleManager<MongoIdentityRole> _roleManager;
        private readonly IItemService _itemService;

        public AdminController(UserManager<Users> userManager, RoleManager<MongoIdentityRole> roleManager, IItemService itemService)
        {
            _userManager = userManager;
            _roleManager = roleManager;
            _itemService = itemService;
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
    }
}