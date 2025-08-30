using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using ServConnect.Models;
using ServConnect.Services;

namespace ServConnect.Controllers
{
    [Authorize(Roles = RoleTypes.ServiceProvider)]
    public class ServiceProviderController : Controller
    {
        private readonly UserManager<Users> _userManager;
        private readonly IItemService _itemService;

        public ServiceProviderController(UserManager<Users> userManager, IItemService itemService)
        {
            _userManager = userManager;
            _itemService = itemService;
        }

        public async Task<IActionResult> Dashboard()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null)
            {
                return NotFound();
            }

            var myItems = await _itemService.GetByOwnerAsync(user.Id);
            ViewBag.UserName = user.FullName;
            ViewBag.TotalServices = myItems?.Count ?? 0;
            ViewBag.ActiveServices = myItems?.Count(i => i.IsActive) ?? 0;
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