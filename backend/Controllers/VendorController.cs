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
        private readonly IGasSubscriptionService _gasService;

        public VendorController(
            UserManager<Users> userManager, 
            IItemService itemService,
            IGasSubscriptionService gasService)
        {
            _userManager = userManager;
            _itemService = itemService;
            _gasService = gasService;
        }

        public async Task<IActionResult> Dashboard()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null)
            {
                return NotFound();
            }

            var myItems = await _itemService.GetByOwnerAsync(user.Id);
            var gasOrders = await _gasService.GetPendingVendorOrdersAsync(user.Id);
            
            ViewBag.UserName = user.FullName;
            ViewBag.TotalItems = myItems?.Count ?? 0;
            ViewBag.ActiveItems = myItems?.Count(i => i.IsActive) ?? 0;
            ViewBag.PendingGasOrders = gasOrders?.Count ?? 0;
            ViewBag.IsGasVendor = user.IsGasVendor || string.Equals(user.VendorCategory, "Gas", StringComparison.OrdinalIgnoreCase);
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

        /// <summary>
        /// Gas cylinder orders from IoT auto-booking system
        /// </summary>
        public async Task<IActionResult> GasOrders()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null)
            {
                return NotFound();
            }

            var orders = await _gasService.GetVendorOrdersAsync(user.Id, 100);
            return View(orders);
        }

        /// <summary>
        /// Vendor settings page
        /// </summary>
        public async Task<IActionResult> Settings()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null)
            {
                return NotFound();
            }
            return View(user);
        }

        /// <summary>
        /// Update vendor settings
        /// </summary>
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UpdateSettings(string? BusinessName, string? BusinessRegistrationNumber, 
            string? BusinessAddress, string? VendorCategory, bool IsGasVendor = false, 
            string? GasCylinderBrand = null, string? GasBusinessLicense = null)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null)
            {
                return NotFound();
            }

            user.BusinessName = BusinessName;
            user.BusinessRegistrationNumber = BusinessRegistrationNumber;
            user.BusinessAddress = BusinessAddress;
            user.VendorCategory = VendorCategory;
            
            // Gas vendor specific fields
            user.IsGasVendor = IsGasVendor;
            if (IsGasVendor)
            {
                user.GasCylinderBrand = GasCylinderBrand;
                user.GasBusinessLicense = GasBusinessLicense;
                user.VendorCategory = "Gas"; // Ensure category is set for backward compatibility
            }
            else
            {
                // Clear gas-specific fields if not a gas vendor
                user.GasCylinderBrand = null;
                user.GasBusinessLicense = null;
            }

            var result = await _userManager.UpdateAsync(user);
            if (result.Succeeded)
            {
                TempData["Success"] = "Settings updated successfully!";
            }
            else
            {
                TempData["Error"] = "Failed to update settings.";
            }

            return RedirectToAction("Settings");
        }

        public IActionResult Profile()
        {
            return RedirectToAction("Profile", "Account");
        }
    }
}