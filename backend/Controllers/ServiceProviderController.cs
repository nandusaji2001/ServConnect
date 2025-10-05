using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using ServConnect.Models;
using ServConnect.Services;

namespace ServConnect.Controllers
{
    [Authorize(Roles = RoleTypes.ServiceProvider)]
    [ServiceFilter(typeof(ServConnect.Filters.RequireApprovedUserFilter))]
    public class ServiceProviderController : Controller
    {
        private readonly UserManager<Users> _userManager;
        private readonly IItemService _itemService;
        private readonly IBookingService _bookingService;
        private readonly IServiceCatalog _catalog;
        private readonly INotificationService _notificationService;

        public ServiceProviderController(UserManager<Users> userManager, IItemService itemService, IBookingService bookingService, IServiceCatalog catalog, INotificationService notificationService)
        {
            _userManager = userManager;
            _itemService = itemService;
            _bookingService = bookingService;
            _catalog = catalog;
            _notificationService = notificationService;
        }

        public async Task<IActionResult> Dashboard()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null)
            {
                return NotFound();
            }

            // Count services via ProviderServices, not Items
            var myLinks = await _catalog.GetProviderLinksByProviderAsync(user.Id);
            ViewBag.UserName = user.FullName;
            ViewBag.TotalServices = myLinks?.Count ?? 0;
            ViewBag.ActiveServices = myLinks?.Count(i => i.IsActive) ?? 0;

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
            ViewBag.UnrespondedCount = bookings.Count(b => b.Status == BookingStatus.Pending);

            // Recent completed services with ratings
            var completedRecent = bookings
                .Where(b => b.IsCompleted)
                .OrderByDescending(b => b.CompletedAtUtc ?? DateTime.MinValue)
                .Take(5)
                .Select(b => new { b.ServiceName, b.UserName, b.CompletedAtUtc, b.UserRating })
                .ToList();
            ViewBag.CompletedRecent = completedRecent;

            // Get real notifications
            var notifications = await _notificationService.GetUserNotificationsAsync(user.Id.ToString(), 5);
            var unreadCount = await _notificationService.GetUnreadCountAsync(user.Id.ToString());
            
            ViewBag.Notifications = notifications;
            ViewBag.UnreadNotificationCount = unreadCount;

            return View();
        }

        public IActionResult Services()
        {
            // Redirect legacy route to the new ProviderServices management UI
            return RedirectToAction("Manage", "Services");
        }

        public IActionResult Profile()
        {
            return RedirectToAction("Profile", "Account");
        }
    }
}