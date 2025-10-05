using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using ServConnect.Models;
using ServConnect.Services;

namespace ServConnect.Controllers
{
    [Authorize]
    [Route("api/[controller]")]
    [ApiController]
    public class NotificationController : ControllerBase
    {
        private readonly UserManager<Users> _userManager;
        private readonly INotificationService _notificationService;

        public NotificationController(UserManager<Users> userManager, INotificationService notificationService)
        {
            _userManager = userManager;
            _notificationService = notificationService;
        }

        [HttpGet]
        public async Task<IActionResult> GetNotifications(int limit = 10)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null)
            {
                return Unauthorized();
            }

            var notifications = await _notificationService.GetUserNotificationsAsync(user.Id.ToString(), limit);
            return Ok(notifications);
        }

        [HttpGet("unread-count")]
        public async Task<IActionResult> GetUnreadCount()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null)
            {
                return Unauthorized();
            }

            var count = await _notificationService.GetUnreadCountAsync(user.Id.ToString());
            return Ok(new { count });
        }

        [HttpPost("{id}/mark-read")]
        public async Task<IActionResult> MarkAsRead(string id)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null)
            {
                return Unauthorized();
            }

            await _notificationService.MarkAsReadAsync(id);
            return Ok();
        }

        [HttpPost("mark-all-read")]
        public async Task<IActionResult> MarkAllAsRead()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null)
            {
                return Unauthorized();
            }

            await _notificationService.MarkAllAsReadAsync(user.Id.ToString());
            return Ok();
        }

        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteNotification(string id)
        {
            await _notificationService.DeleteNotificationAsync(id);
            return Ok();
        }

        // Demo endpoint to create sample notifications for testing
        [HttpPost("demo")]
        [Authorize(Roles = RoleTypes.ServiceProvider)]
        public async Task<IActionResult> CreateDemoNotifications()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null)
            {
                return Unauthorized();
            }

            // Create some sample notifications
            await _notificationService.CreateNotificationAsync(
                user.Id.ToString(),
                "New Booking Request",
                "You have a new booking request for House Cleaning from John Doe",
                NotificationType.BookingReceived,
                actionUrl: "/provider/bookings"
            );

            await _notificationService.CreateNotificationAsync(
                user.Id.ToString(),
                "Payment Received",
                "You received ₹2,500 payment for Plumbing Service",
                NotificationType.PaymentReceived
            );

            await _notificationService.CreateNotificationAsync(
                user.Id.ToString(),
                "New Review Received",
                "Sarah Johnson gave you 5 stars for Electrical Repair service",
                NotificationType.ReviewReceived
            );

            await _notificationService.CreateNotificationAsync(
                user.Id.ToString(),
                "Service Expiring Soon",
                "Your service 'Garden Maintenance' will expire in 3 days",
                NotificationType.ServiceExpiring,
                actionUrl: "/Services/Manage"
            );

            return Ok(new { message = "Demo notifications created successfully!" });
        }
    }
}
