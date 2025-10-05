using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using ServConnect.Models;
using ServConnect.Services;

namespace ServConnect.Controllers
{
    [Authorize(Roles = RoleTypes.ServiceProvider)]
    [ServiceFilter(typeof(ServConnect.Filters.RequireApprovedUserFilter))]
    public class AnalyticsController : Controller
    {
        private readonly UserManager<Users> _userManager;
        private readonly IBookingService _bookingService;
        private readonly IServiceCatalog _catalog;

        public AnalyticsController(UserManager<Users> userManager, IBookingService bookingService, IServiceCatalog catalog)
        {
            _userManager = userManager;
            _bookingService = bookingService;
            _catalog = catalog;
        }

        public async Task<IActionResult> Index()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null)
            {
                return NotFound();
            }

            // Get comprehensive analytics data
            var myLinks = await _catalog.GetProviderLinksByProviderAsync(user.Id);
            var bookings = await _bookingService.GetForProviderAsync(user.Id);

            // Basic service stats
            ViewBag.UserName = user.FullName;
            ViewBag.TotalServices = myLinks?.Count ?? 0;
            ViewBag.ActiveServices = myLinks?.Count(i => i.IsActive) ?? 0;
            ViewBag.InactiveServices = (myLinks?.Count ?? 0) - (myLinks?.Count(i => i.IsActive) ?? 0);

            // Booking analytics
            var totalBookings = bookings.Count;
            var pendingBookings = bookings.Count(b => b.Status == BookingStatus.Pending);
            var acceptedBookings = bookings.Count(b => b.Status == BookingStatus.Accepted);
            var completedBookings = bookings.Count(b => b.IsCompleted);
            var rejectedBookings = bookings.Count(b => b.Status == BookingStatus.Rejected);

            ViewBag.TotalBookings = totalBookings;
            ViewBag.PendingBookings = pendingBookings;
            ViewBag.AcceptedBookings = acceptedBookings;
            ViewBag.CompletedBookings = completedBookings;
            ViewBag.RejectedBookings = rejectedBookings;

            // Time-based analytics
            var now = DateTime.UtcNow;
            var thisWeek = bookings.Count(b => b.RequestedAtUtc >= now.AddDays(-7));
            var thisMonth = bookings.Count(b => b.RequestedAtUtc >= now.AddDays(-30));
            var last30Days = bookings.Where(b => b.RequestedAtUtc >= now.AddDays(-30)).ToList();

            ViewBag.BookingsThisWeek = thisWeek;
            ViewBag.BookingsThisMonth = thisMonth;

            // Performance metrics
            var responded = bookings.Count(b => b.Status != BookingStatus.Pending);
            var responseRate = totalBookings > 0 ? Math.Round((double)responded * 100 / totalBookings, 1) : 0;
            var completionRate = totalBookings > 0 ? Math.Round((double)completedBookings * 100 / totalBookings, 1) : 0;

            ViewBag.ResponseRate = responseRate;
            ViewBag.CompletionRate = completionRate;

            // Rating analytics
            var ratedBookings = bookings.Where(b => b.IsCompleted && b.UserRating.HasValue).ToList();
            var avgRating = ratedBookings.Count > 0 ? Math.Round(ratedBookings.Average(b => b.UserRating!.Value), 1) : 0;
            var totalReviews = ratedBookings.Count;

            ViewBag.AvgRating = avgRating;
            ViewBag.TotalReviews = totalReviews;

            // Rating distribution
            var ratingDistribution = new Dictionary<int, int>();
            for (int i = 1; i <= 5; i++)
            {
                ratingDistribution[i] = ratedBookings.Count(b => b.UserRating == i);
            }
            ViewBag.RatingDistribution = ratingDistribution;

            // Recent completed services with ratings
            var recentCompleted = bookings
                .Where(b => b.IsCompleted)
                .OrderByDescending(b => b.CompletedAtUtc ?? DateTime.MinValue)
                .Take(10)
                .Select(b => new { 
                    b.ServiceName, 
                    b.UserName, 
                    b.CompletedAtUtc, 
                    b.UserRating,
                    b.RequestedAtUtc,
                    b.Status
                })
                .ToList();
            ViewBag.RecentCompleted = recentCompleted;

            // Monthly booking trends (last 6 months)
            var monthlyTrends = new List<object>();
            for (int i = 5; i >= 0; i--)
            {
                var monthStart = now.AddMonths(-i).Date.AddDays(1 - now.AddMonths(-i).Day);
                var monthEnd = monthStart.AddMonths(1).AddDays(-1);
                var monthBookings = bookings.Count(b => b.RequestedAtUtc >= monthStart && b.RequestedAtUtc <= monthEnd);
                monthlyTrends.Add(new { 
                    Month = monthStart.ToString("MMM yyyy"), 
                    Bookings = monthBookings 
                });
            }
            ViewBag.MonthlyTrends = monthlyTrends;

            // Service performance
            var servicePerformance = myLinks?.Select(service => {
                var serviceBookings = bookings.Where(b => b.ServiceName == service.ServiceName).ToList();
                var serviceCompleted = serviceBookings.Count(b => b.IsCompleted);
                var serviceRating = serviceBookings.Where(b => b.UserRating.HasValue).Average(b => b.UserRating) ?? 0;
                
                return new {
                    ServiceName = service.ServiceName,
                    TotalBookings = serviceBookings.Count,
                    CompletedBookings = serviceCompleted,
                    AvgRating = Math.Round(serviceRating, 1),
                    IsActive = service.IsActive
                };
            }).OrderByDescending(s => s.TotalBookings).ToList();

            ViewBag.ServicePerformance = servicePerformance;

            return View();
        }
    }
}
