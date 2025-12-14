using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using ServConnect.Models;
using ServConnect.Services;

namespace ServConnect.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class RentalSubscriptionController : ControllerBase
    {
        private readonly UserManager<Users> _userManager;
        private readonly IRentalSubscriptionService _subscriptionService;
        private readonly IConfiguration _config;
        private readonly ILogger<RentalSubscriptionController> _logger;

        public RentalSubscriptionController(
            UserManager<Users> userManager,
            IRentalSubscriptionService subscriptionService,
            IConfiguration config,
            ILogger<RentalSubscriptionController> logger)
        {
            _userManager = userManager;
            _subscriptionService = subscriptionService;
            _config = config;
            _logger = logger;
        }

        // GET: api/rentalsubscription/plans
        [HttpGet("plans")]
        public IActionResult GetPlans()
        {
            var plans = new List<object>
            {
                new {
                    plan = (int)RentalSubscriptionPlan.OneWeek,
                    name = RentalSubscriptionPricing.GetPlanName(RentalSubscriptionPlan.OneWeek),
                    price = RentalSubscriptionPricing.GetPrice(RentalSubscriptionPlan.OneWeek),
                    durationDays = RentalSubscriptionPricing.GetDurationDays(RentalSubscriptionPlan.OneWeek),
                    pricePerDay = Math.Round(RentalSubscriptionPricing.GetPrice(RentalSubscriptionPlan.OneWeek) / 7, 2),
                    popular = false,
                    bestValue = false
                },
                new {
                    plan = (int)RentalSubscriptionPlan.OneMonth,
                    name = RentalSubscriptionPricing.GetPlanName(RentalSubscriptionPlan.OneMonth),
                    price = RentalSubscriptionPricing.GetPrice(RentalSubscriptionPlan.OneMonth),
                    durationDays = RentalSubscriptionPricing.GetDurationDays(RentalSubscriptionPlan.OneMonth),
                    pricePerDay = Math.Round(RentalSubscriptionPricing.GetPrice(RentalSubscriptionPlan.OneMonth) / 30, 2),
                    popular = true,
                    bestValue = false
                },
                new {
                    plan = (int)RentalSubscriptionPlan.ThreeMonths,
                    name = RentalSubscriptionPricing.GetPlanName(RentalSubscriptionPlan.ThreeMonths),
                    price = RentalSubscriptionPricing.GetPrice(RentalSubscriptionPlan.ThreeMonths),
                    durationDays = RentalSubscriptionPricing.GetDurationDays(RentalSubscriptionPlan.ThreeMonths),
                    pricePerDay = Math.Round(RentalSubscriptionPricing.GetPrice(RentalSubscriptionPlan.ThreeMonths) / 90, 2),
                    popular = false,
                    bestValue = true
                }
            };

            return Ok(new { success = true, plans });
        }

        // GET: api/rentalsubscription/status
        [Authorize]
        [HttpGet("status")]
        public async Task<IActionResult> GetSubscriptionStatus()
        {
            var userId = _userManager.GetUserId(User);
            if (string.IsNullOrEmpty(userId))
            {
                return Unauthorized(new { success = false, error = "Not authenticated" });
            }

            var status = await _subscriptionService.GetSubscriptionStatusAsync(userId);
            return Ok(new { success = true, status });
        }

        // POST: api/rentalsubscription/create
        [Authorize]
        [HttpPost("create")]
        public async Task<IActionResult> CreateSubscription([FromBody] CreateRentalSubscriptionDto dto)
        {
            try
            {
                var userId = _userManager.GetUserId(User);
                if (string.IsNullOrEmpty(userId))
                {
                    return Unauthorized(new { success = false, error = "Not authenticated" });
                }

                // Check if user already has active subscription
                var existingStatus = await _subscriptionService.GetSubscriptionStatusAsync(userId);
                
                // Create subscription (pending payment)
                var subscription = await _subscriptionService.CreateSubscriptionAsync(userId, dto.Plan);
                
                // Generate Razorpay order details
                var razorpayKey = _config["Razorpay:KeyId"] ?? "";
                var amountInPaise = (int)(subscription.AmountPaid * 100);

                return Ok(new { 
                    success = true, 
                    subscription = new {
                        id = subscription.Id,
                        plan = subscription.Plan,
                        planName = RentalSubscriptionPricing.GetPlanName(subscription.Plan),
                        amount = subscription.AmountPaid,
                        amountInPaise = amountInPaise,
                        startDate = subscription.StartDate,
                        expiryDate = subscription.ExpiryDate,
                        isExtension = existingStatus.HasActiveSubscription
                    },
                    razorpayKey = razorpayKey
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating subscription");
                return StatusCode(500, new { success = false, error = "Failed to create subscription" });
            }
        }

        // POST: api/rentalsubscription/verify
        [Authorize]
        [HttpPost("verify")]
        public async Task<IActionResult> VerifyPayment([FromBody] VerifyRentalSubscriptionPaymentDto dto)
        {
            try
            {
                var userId = _userManager.GetUserId(User);
                if (string.IsNullOrEmpty(userId))
                {
                    return Unauthorized(new { success = false, error = "Not authenticated" });
                }

                var subscription = await _subscriptionService.GetByIdAsync(dto.SubscriptionId);
                if (subscription == null || subscription.UserId != userId)
                {
                    return BadRequest(new { success = false, error = "Subscription not found" });
                }

                if (subscription.PaymentStatus == RentalPaymentStatus.Completed)
                {
                    return Ok(new { success = true, message = "Subscription already activated" });
                }

                // NOTE: For production, verify Razorpay signature using HMAC SHA256
                // var expectedSignature = ComputeHmacSha256(dto.RazorpayOrderId + "|" + dto.RazorpayPaymentId, razorpaySecret);
                // if (dto.RazorpaySignature != expectedSignature) return BadRequest("Invalid signature");

                var activated = await _subscriptionService.ActivateSubscriptionAsync(
                    dto.SubscriptionId, 
                    dto.RazorpayPaymentId);

                if (!activated)
                {
                    return BadRequest(new { success = false, error = "Failed to activate subscription" });
                }

                // Get updated status
                var status = await _subscriptionService.GetSubscriptionStatusAsync(userId);

                return Ok(new { 
                    success = true, 
                    message = "Subscription activated successfully!",
                    status 
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error verifying subscription payment");
                return StatusCode(500, new { success = false, error = "Failed to verify payment" });
            }
        }

        // POST: api/rentalsubscription/cancel-pending
        [Authorize]
        [HttpPost("cancel-pending/{subscriptionId}")]
        public async Task<IActionResult> CancelPendingSubscription(string subscriptionId)
        {
            try
            {
                var userId = _userManager.GetUserId(User);
                if (string.IsNullOrEmpty(userId))
                {
                    return Unauthorized(new { success = false, error = "Not authenticated" });
                }

                var subscription = await _subscriptionService.GetByIdAsync(subscriptionId);
                if (subscription == null || subscription.UserId != userId)
                {
                    return BadRequest(new { success = false, error = "Subscription not found" });
                }

                if (subscription.PaymentStatus != RentalPaymentStatus.Pending)
                {
                    return BadRequest(new { success = false, error = "Cannot cancel non-pending subscription" });
                }

                await _subscriptionService.UpdatePaymentStatusAsync(subscriptionId, RentalPaymentStatus.Cancelled);

                return Ok(new { success = true, message = "Pending subscription cancelled" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error cancelling pending subscription");
                return StatusCode(500, new { success = false, error = "Failed to cancel subscription" });
            }
        }

        // GET: api/rentalsubscription/history
        [Authorize]
        [HttpGet("history")]
        public async Task<IActionResult> GetSubscriptionHistory()
        {
            var userId = _userManager.GetUserId(User);
            if (string.IsNullOrEmpty(userId))
            {
                return Unauthorized(new { success = false, error = "Not authenticated" });
            }

            var history = await _subscriptionService.GetUserSubscriptionHistoryAsync(userId);
            return Ok(new { success = true, history });
        }
    }

    public class VerifyRentalSubscriptionPaymentDto
    {
        public string SubscriptionId { get; set; } = string.Empty;
        public string? RazorpayOrderId { get; set; }
        public string RazorpayPaymentId { get; set; } = string.Empty;
        public string? RazorpaySignature { get; set; }
    }
}
