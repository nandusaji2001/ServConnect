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
        private readonly IBookingService _bookingService;

        public ServiceProviderController(UserManager<Users> userManager, IItemService itemService, IBookingService bookingService)
        {
            _userManager = userManager;
            _itemService = itemService;
            _bookingService = bookingService;
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

            // Booking analytics for this provider
            var bookings = await _bookingService.GetForProviderAsync(user.Id);
            var total = bookings.Count;
            var responded = bookings.Count(b => b.Status != BookingStatus.Pending);
            var completed = bookings.Count(b => b.IsCompleted);
            var thisWeek = bookings.Count(b => b.RequestedAtUtc >= DateTime.UtcNow.AddDays(-7));
            var ratingList = bookings.Where(b => b.IsCompleted && b.UserRating.HasValue).Select(b => b.UserRating!.Value).ToList();
            double avgRating = ratingList.Count > 0 ? ratingList.Average() : 0;

            ViewBag.BookingsThisWeek = thisWeek;
            ViewBag.ResponseRatePercent = total > 0 ? Math.Round((double)responded * 100 / total, 1) : 0;
            ViewBag.CompletionRatePercent = total > 0 ? Math.Round((double)completed * 100 / total, 1) : 0;
            ViewBag.AvgRating = Math.Round(avgRating, 1);

            // Recent completed services with ratings
            var completedRecent = bookings
                .Where(b => b.IsCompleted)
                .OrderByDescending(b => b.CompletedAtUtc ?? DateTime.MinValue)
                .Take(5)
                .Select(b => new { b.ServiceName, b.UserName, b.CompletedAtUtc, b.UserRating })
                .ToList();
            ViewBag.CompletedRecent = completedRecent;

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