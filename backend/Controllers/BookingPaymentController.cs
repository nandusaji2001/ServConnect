using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using ServConnect.Models;
using ServConnect.Services;

namespace ServConnect.Controllers
{
    public class BookingPaymentController : Controller
    {
        private readonly IBookingPaymentService _paymentService;
        private readonly IBookingService _bookingService;
        private readonly UserManager<Users> _userManager;
        private readonly IConfiguration _configuration;

        public BookingPaymentController(IBookingPaymentService paymentService, IBookingService bookingService, UserManager<Users> userManager, IConfiguration configuration)
        {
            _paymentService = paymentService;
            _bookingService = bookingService;
            _userManager = userManager;
            _configuration = configuration;
        }

        [HttpGet("/booking-payment/pay/{paymentId}")]
        [Authorize]
        public async Task<IActionResult> Pay(string paymentId)
        {
            var payment = await _paymentService.GetPaymentByIdAsync(paymentId);
            if (payment == null) return NotFound();

            var user = await _userManager.GetUserAsync(User);
            if (user == null || payment.UserId != user.Id) return Forbid();

            ViewBag.RazorpayKeyId = _configuration["Razorpay:KeyId"];
            return View(payment);
        }

        public class CreatePaymentRequest
        {
            public string BookingId { get; set; } = string.Empty;
            public decimal AmountInRupees { get; set; }
            public int? Rating { get; set; }
            public string? Feedback { get; set; }
        }

        [HttpPost("/api/booking-payment/create")]
        [Authorize]
        public async Task<IActionResult> CreatePayment([FromBody] CreatePaymentRequest request)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return Unauthorized();

            // Verify the booking exists and belongs to the user
            var bookings = await _bookingService.GetForUserAsync(user.Id);
            var booking = bookings.FirstOrDefault(b => b.Id == request.BookingId);
            if (booking == null) return NotFound("Booking not found");

            // Check if payment already exists for this booking
            var existingPayment = await _paymentService.GetPaymentByBookingIdAsync(request.BookingId);
            if (existingPayment != null)
            {
                return Ok(new { paymentId = existingPayment.Id, redirectUrl = $"/booking-payment/pay/{existingPayment.Id}" });
            }

            // Create new payment
            var payment = await _paymentService.CreatePaymentAsync(
                user.Id, 
                user.FullName ?? user.UserName ?? "User", 
                user.Email ?? string.Empty,
                request.BookingId,
                booking.ServiceName,
                booking.ProviderName,
                request.AmountInRupees,
                request.Rating,
                request.Feedback
            );

            return Ok(new { paymentId = payment.Id, redirectUrl = $"/booking-payment/pay/{payment.Id}" });
        }

        public class VerifyPaymentRequest
        {
            public string PaymentId { get; set; } = string.Empty;
            public string RazorpayOrderId { get; set; } = string.Empty;
            public string RazorpayPaymentId { get; set; } = string.Empty;
            public string RazorpaySignature { get; set; } = string.Empty;
        }

        [HttpPost("/api/booking-payment/verify")]
        [Authorize]
        public async Task<IActionResult> VerifyPayment([FromBody] VerifyPaymentRequest request)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return Unauthorized();

            var payment = await _paymentService.GetPaymentByIdAsync(request.PaymentId);
            if (payment == null || payment.UserId != user.Id) return NotFound();

            // Mark payment as paid
            var success = await _paymentService.MarkPaymentPaidAsync(
                request.PaymentId,
                request.RazorpayOrderId,
                request.RazorpayPaymentId,
                request.RazorpaySignature
            );

            if (success)
            {
                // Mark the booking as completed
                await _bookingService.CompleteAsync(payment.BookingId, user.Id, payment.UserRating, payment.UserFeedback);
                return Ok(new { success = true, message = "Payment verified and booking completed successfully!" });
            }

            return BadRequest(new { success = false, message = "Payment verification failed" });
        }

        [HttpGet("/api/booking-payment/pending")]
        [Authorize]
        public async Task<IActionResult> GetPendingPayments()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return Unauthorized();

            var pendingPayments = await _paymentService.GetPendingPaymentsByUserAsync(user.Id);
            return Ok(pendingPayments);
        }

        [HttpGet("/api/booking-payment/has-pending")]
        [Authorize]
        public async Task<IActionResult> HasPendingPayments()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return Unauthorized();

            var hasPending = await _paymentService.HasPendingPaymentsAsync(user.Id);
            return Ok(new { hasPending });
        }
    }
}
