using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using ServConnect.Models;
using ServConnect.Services;

namespace ServConnect.Controllers
{
    public class BookingsController : Controller
    {
        private readonly IBookingService _bookings;
        private readonly UserManager<Users> _userManager;
        private readonly IServiceCatalog _catalog;

        public BookingsController(IBookingService bookings, UserManager<Users> userManager, IServiceCatalog catalog)
        {
            _bookings = bookings;
            _userManager = userManager;
            _catalog = catalog;
        }

        public class CreateBookingRequest
        {
            public string ProviderServiceId { get; set; } = string.Empty; // Mongo ObjectId string of ProviderService
            public Guid ProviderId { get; set; }
            public string ProviderName { get; set; } = string.Empty;
            public string ServiceName { get; set; } = string.Empty;
            public DateTime ServiceDateTime { get; set; }
            public string? Note { get; set; }
        }

        [HttpPost("/api/bookings")] 
        [Authorize] // any logged-in user can request a booking
        [ServiceFilter(typeof(ServConnect.Filters.RequireApprovedUserFilter))]
        public async Task<IActionResult> Create([FromBody] CreateBookingRequest req)
        {
            if (string.IsNullOrWhiteSpace(req.ProviderServiceId)) return BadRequest("Provider service is required");
            if (req.ServiceDateTime <= DateTime.UtcNow.AddMinutes(-1)) return BadRequest("Please choose a future date/time");

            var me = await _userManager.GetUserAsync(User);
            if (me == null) return Unauthorized();

            // Use phone and address from user's profile
            var contactPhone = me.PhoneNumber ?? string.Empty;
            var address = me.Address ?? string.Empty;
            if (string.IsNullOrWhiteSpace(contactPhone) || string.IsNullOrWhiteSpace(address))
            {
                return BadRequest("Please complete your profile (phone and address) before booking.");
            }

            var booking = await _bookings.CreateAsync(
                me.Id, me.FullName ?? me.UserName ?? "User", me.Email ?? string.Empty,
                req.ProviderId, string.IsNullOrWhiteSpace(req.ProviderName) ? "Provider" : req.ProviderName,
                req.ProviderServiceId, string.IsNullOrWhiteSpace(req.ServiceName) ? "Service" : req.ServiceName,
                req.ServiceDateTime, contactPhone, address, req.Note
            );

            return Ok(booking);
        }

        // Provider view: list bookings
        [HttpGet("/provider/bookings")]
        [Authorize(Roles = RoleTypes.ServiceProvider)]
        [ServiceFilter(typeof(ServConnect.Filters.RequireApprovedUserFilter))]
        public async Task<IActionResult> ProviderList()
        {
            var me = await _userManager.GetUserAsync(User);
            if (me == null) return Unauthorized();
            var list = await _bookings.GetForProviderAsync(me.Id);
            ViewBag.UnrespondedCount = list.Count(b => b.Status == BookingStatus.Pending);
            return View("ProviderBookings", list);
        }

        public class DecisionRequest
        {
            public string BookingId { get; set; } = string.Empty;
            public bool Accept { get; set; }
            public string? Message { get; set; }
        }

        // Provider decide accept/reject
        [HttpPost("/api/bookings/decision")] 
        [Authorize(Roles = RoleTypes.ServiceProvider)]
        [ServiceFilter(typeof(ServConnect.Filters.RequireApprovedUserFilter))]
        public async Task<IActionResult> Decide([FromBody] DecisionRequest req)
        {
            var me = await _userManager.GetUserAsync(User);
            if (me == null) return Unauthorized();
            var status = req.Accept ? BookingStatus.Accepted : BookingStatus.Rejected;
            var ok = await _bookings.SetStatusAsync(req.BookingId, me.Id, status, req.Message);
            if (!ok) return NotFound();
            return Ok();
        }

        public class CompleteRequest
        {
            public string BookingId { get; set; } = string.Empty;
            public int? Rating { get; set; } // 1..5 optional
            public string? Feedback { get; set; }
        }

        [HttpPost("/api/bookings/complete")]
        [Authorize]
        [ServiceFilter(typeof(ServConnect.Filters.RequireApprovedUserFilter))]
        public async Task<IActionResult> Complete([FromBody] CompleteRequest req)
        {
            var me = await _userManager.GetUserAsync(User);
            if (me == null) return Unauthorized();
            var ok = await _bookings.CompleteAsync(req.BookingId, me.Id, req.Rating, req.Feedback);
            if (!ok) return NotFound();
            return Ok();
        }

        // User view: my bookings
        [HttpGet("/my/bookings")] 
        [Authorize]
        public async Task<IActionResult> MyBookings()
        {
            var me = await _userManager.GetUserAsync(User);
            if (me == null) return Unauthorized();
            var list = await _bookings.GetForUserAsync(me.Id);
            return View("UserBookings", list);
        }
    }
}