using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using ServConnect.Models;

namespace ServConnect.Controllers
{
    [Authorize(Roles = RoleTypes.ServiceProvider)]
    public class ServiceProviderController : Controller
    {
        private readonly UserManager<Users> _userManager;

        public ServiceProviderController(UserManager<Users> userManager)
        {
            _userManager = userManager;
        }

        public async Task<IActionResult> Dashboard()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null)
            {
                return NotFound();
            }

            ViewBag.UserName = user.FullName;
            return View();
        }

        public IActionResult Services()
        {
            // Placeholder for managing services
            return View();
        }

        public IActionResult Profile()
        {
            return RedirectToAction("Profile", "Account");
        }
    }
}