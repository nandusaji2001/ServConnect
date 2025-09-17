using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using ServConnect.Models;
using ServConnect.Services;

namespace ServConnect.Controllers
{
    [Authorize(Roles = RoleTypes.Vendor)]
    [ServiceFilter(typeof(ServConnect.Filters.RequireApprovedUserFilter))]
    public class VendorController : Controller
    {
        private readonly UserManager<Users> _userManager;
        private readonly IItemService _itemService;

        public VendorController(UserManager<Users> userManager, IItemService itemService)
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
            ViewBag.TotalItems = myItems?.Count ?? 0;
            ViewBag.ActiveItems = myItems?.Count(i => i.IsActive) ?? 0;
            return View();
        }

        public IActionResult Products()
        {
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