using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using ServConnect.Models;
using ServConnect.Services;

namespace ServConnect.Controllers
{
    [Authorize(Roles = RoleTypes.ServiceProvider)]
    [ServiceFilter(typeof(ServConnect.Filters.RequireApprovedUserFilter))]
    public class ServicePaymentController : Controller
    {
        private readonly UserManager<Users> _userManager;
        private readonly IServicePaymentService _paymentService;
        private readonly IConfiguration _config;

        public ServicePaymentController(
            UserManager<Users> userManager,
            IServicePaymentService paymentService,
            IConfiguration config)
        {
            _userManager = userManager;
            _paymentService = paymentService;
            _config = config;
        }

        // API: Get publication plans
        [HttpGet("/api/service-payment/plans")]
        public async Task<IActionResult> GetPlans()
        {
            var plans = await _paymentService.GetPublicationPlansAsync();
            return Ok(plans);
        }

        // API: Create payment for service publication
        [HttpPost("/api/service-payment/create")]
        public async Task<IActionResult> CreatePayment([FromBody] CreatePaymentRequest request)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return Unauthorized();

            if (string.IsNullOrWhiteSpace(request.ServiceName) || 
                string.IsNullOrWhiteSpace(request.PlanName) ||
                request.DurationInMonths <= 0 ||
                request.AmountInRupees <= 0)
            {
                return BadRequest("Invalid payment request");
            }

            var payment = await _paymentService.CreatePaymentAsync(
                user.Id, 
                request.ServiceName, 
                request.PlanName, 
                request.DurationInMonths, 
                request.AmountInRupees);

            return Ok(new { paymentId = payment.Id, amountInPaise = payment.AmountInPaise });
        }

        // Payment page
        [HttpGet("/service-payment/pay/{paymentId}")]
        public async Task<IActionResult> Pay(string paymentId)
        {
            var payment = await _paymentService.GetPaymentByIdAsync(paymentId);
            if (payment == null) return NotFound();

            var user = await _userManager.GetUserAsync(User);
            if (user == null || payment.ProviderId != user.Id) return Unauthorized();

            ViewBag.RazorpayKey = _config["Razorpay:KeyId"] ?? "";
            ViewBag.Payment = payment;
            return View();
        }

        // Verify payment
        [HttpPost("/api/service-payment/verify")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> VerifyPayment([FromBody] VerifyPaymentRequest request)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return Unauthorized();

            if (string.IsNullOrWhiteSpace(request.PaymentId) || 
                string.IsNullOrWhiteSpace(request.RazorpayPaymentId))
            {
                return BadRequest("Invalid payment verification request");
            }

            var payment = await _paymentService.GetPaymentByIdAsync(request.PaymentId);
            if (payment == null || payment.ProviderId != user.Id)
                return BadRequest("Payment not found");

            // NOTE: For production, verify signature using Razorpay secret (HMAC SHA256)
            var success = await _paymentService.MarkPaymentPaidAsync(
                request.PaymentId,
                request.RazorpayOrderId ?? "",
                request.RazorpayPaymentId,
                request.RazorpaySignature ?? "");

            if (!success)
                return BadRequest("Payment verification failed");

            return Ok(new { success = true, message = "Payment verified successfully" });
        }

        // Activate service after payment
        [HttpPost("/api/service-payment/activate")]
        public async Task<IActionResult> ActivateService([FromBody] ActivateServiceRequest request)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return Unauthorized();

            var payment = await _paymentService.GetPaymentByIdAsync(request.PaymentId);
            if (payment == null || payment.ProviderId != user.Id)
                return BadRequest("Payment not found");

            var success = await _paymentService.ActivateServiceAsync(request.PaymentId, request.ProviderServiceId);
            if (!success)
                return BadRequest("Failed to activate service");

            return Ok(new { success = true, message = "Service activated successfully" });
        }

        // Get payment history
        [HttpGet("/api/service-payment/history")]
        public async Task<IActionResult> GetPaymentHistory()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return Unauthorized();

            var payments = await _paymentService.GetPaymentsByProviderAsync(user.Id);
            return Ok(payments);
        }
    }

    public class CreatePaymentRequest
    {
        public string ServiceName { get; set; } = string.Empty;
        public string PlanName { get; set; } = string.Empty;
        public int DurationInMonths { get; set; }
        public decimal AmountInRupees { get; set; }
    }

    public class VerifyPaymentRequest
    {
        public string PaymentId { get; set; } = string.Empty;
        public string? RazorpayOrderId { get; set; }
        public string RazorpayPaymentId { get; set; } = string.Empty;
        public string? RazorpaySignature { get; set; }
    }

    public class ActivateServiceRequest
    {
        public string PaymentId { get; set; } = string.Empty;
        public string ProviderServiceId { get; set; } = string.Empty;
    }
}
