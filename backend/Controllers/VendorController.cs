using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using ServConnect.Models;

namespace ServConnect.Controllers
{
    [Authorize(Roles = RoleTypes.Vendor)]
    public class VendorController : Controller
    {
        private readonly UserManager<Users> _userManager;

        public VendorController(UserManager<Users> userManager)
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

        public IActionResult Products()
        {
            // Placeholder for managing products
            return View();
        }

        public IActionResult Orders()
        {
            // Placeholder for managing orders
            return View();
        }

        public IActionResult Profile()
        {
            return RedirectToAction("Profile", "Account");
        }
    }
}