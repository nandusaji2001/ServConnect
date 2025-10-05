using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using ServConnect.Models;
using ServConnect.Services;

namespace ServConnect.Controllers
{
    [Authorize(Roles = RoleTypes.ServiceProvider)]
    [ServiceFilter(typeof(ServConnect.Filters.RequireApprovedUserFilter))]
    public class NotificationsController : Controller
    {
        private readonly UserManager<Users> _userManager;
        private readonly INotificationService _notificationService;

        public NotificationsController(UserManager<Users> userManager, INotificationService notificationService)
        {
            _userManager = userManager;
            _notificationService = notificationService;
        }

        public async Task<IActionResult> Index()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null)
            {
                return NotFound();
            }

            // Get all notifications for the user
            var notifications = await _notificationService.GetUserNotificationsAsync(user.Id.ToString(), 50);
            var unreadCount = await _notificationService.GetUnreadCountAsync(user.Id.ToString());
            
            ViewBag.UserName = user.FullName;
            ViewBag.Notifications = notifications;
            ViewBag.UnreadCount = unreadCount;
            ViewBag.TotalNotifications = notifications.Count;

            return View();
        }
    }
}
