using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using ServConnect.Models;
using ServConnect.Services;

namespace ServConnect.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class OrdersController : ControllerBase
    {
        private readonly IOrderService _orders;
        private readonly IItemService _items;
        private readonly UserManager<Users> _userManager;
        private readonly IConfiguration _config;

        public OrdersController(IOrderService orders, IItemService items, UserManager<Users> userManager, IConfiguration config)
        {
            _orders = orders;
            _items = items;
            _userManager = userManager;
            _config = config;
        }

        public class CreateOrderInput
        {
            public string ItemId { get; set; } = string.Empty;
            public int Quantity { get; set; }
            public string ShippingAddress { get; set; } = string.Empty;
        }

        [HttpPost]
        [Authorize]
        [ServiceFilter(typeof(ServConnect.Filters.RequireApprovedUserFilter))]
        public async Task<IActionResult> Create([FromBody] CreateOrderInput input)
        {
            if (string.IsNullOrWhiteSpace(input.ItemId) || input.Quantity <= 0)
                return BadRequest("Invalid item or quantity");

            var me = await _userManager.GetUserAsync(User);
            if (me == null) return Unauthorized();

            // Create order and generate (placeholder) Razorpay order id
            var (order, razorpayOrderId) = await _orders.CreateOrderAsync(me.Id, me.Email ?? string.Empty, input.ItemId, input.Quantity, input.ShippingAddress);

            // Return data needed for client-side Razorpay checkout
            var keyId = _config["Razorpay:KeyId"] ?? string.Empty;
            return Ok(new
            {
                orderId = order.Id,
                razorpayOrderId,
                amount = (long)(order.TotalAmount * 100),
                currency = "INR",
                key = keyId,
                description = order.ItemTitle
            });
        }

        public class PaymentVerifyInput
        {
            public string OrderId { get; set; } = string.Empty; // our mongo order id
            public string RazorpayOrderId { get; set; } = string.Empty;
            public string RazorpayPaymentId { get; set; } = string.Empty;
            public string RazorpaySignature { get; set; } = string.Empty;
        }

        [HttpPost("verify-payment")]
        [Authorize]
        public async Task<IActionResult> VerifyPayment([FromBody] PaymentVerifyInput input)
        {
            var me = await _userManager.GetUserAsync(User);
            if (me == null) return Unauthorized();

            var ok = await _orders.VerifyPaymentAndConfirmAsync(input.OrderId, input.RazorpayOrderId, input.RazorpayPaymentId, input.RazorpaySignature);
            if (!ok) return BadRequest("Payment verification failed");
            return Ok(new { success = true });
        }

        [HttpGet("mine")]
        [Authorize]
        public async Task<IActionResult> MyOrders()
        {
            var me = await _userManager.GetUserAsync(User);
            if (me == null) return Unauthorized();
            var list = await _orders.GetOrdersForUserAsync(me.Id);
            return Ok(list);
        }

        [HttpGet("vendor")]
        [Authorize(Roles = $"{RoleTypes.Vendor},{RoleTypes.ServiceProvider}")]
        [ServiceFilter(typeof(ServConnect.Filters.RequireApprovedUserFilter))]
        public async Task<IActionResult> VendorOrders()
        {
            var me = await _userManager.GetUserAsync(User);
            if (me == null) return Unauthorized();
            var list = await _orders.GetOrdersForVendorAsync(me.Id);
            return Ok(list);
        }

        public class ShipInput { public string OrderId { get; set; } = string.Empty; public string? TrackingUrl { get; set; } }

        [HttpPost("ship")]
        [Authorize(Roles = $"{RoleTypes.Vendor},{RoleTypes.ServiceProvider}")]
        [ServiceFilter(typeof(ServConnect.Filters.RequireApprovedUserFilter))]
        public async Task<IActionResult> MarkShipped([FromBody] ShipInput input)
        {
            var me = await _userManager.GetUserAsync(User);
            if (me == null) return Unauthorized();
            var ok = await _orders.MarkShippedAsync(input.OrderId, me.Id, input.TrackingUrl);
            return ok ? Ok() : BadRequest();
        }

        public class TrackingInput { public string OrderId { get; set; } = string.Empty; public string TrackingUrl { get; set; } = string.Empty; }

        [HttpPost("set-tracking")]
        [Authorize(Roles = $"{RoleTypes.Vendor},{RoleTypes.ServiceProvider}")]
        public async Task<IActionResult> SetTracking([FromBody] TrackingInput input)
        {
            var me = await _userManager.GetUserAsync(User);
            if (me == null) return Unauthorized();
            var ok = await _orders.SetTrackingUrlAsync(input.OrderId, me.Id, input.TrackingUrl);
            return ok ? Ok() : BadRequest();
        }

        public class DeliverInput { public string OrderId { get; set; } = string.Empty; }

        [HttpPost("deliver")]
        [Authorize]
        public async Task<IActionResult> MarkDelivered([FromBody] DeliverInput input)
        {
            var me = await _userManager.GetUserAsync(User);
            if (me == null) return Unauthorized();
            var ok = await _orders.MarkDeliveredAsync(input.OrderId, me.Id);
            return ok ? Ok() : BadRequest();
        }

        public class CancelInput { public string OrderId { get; set; } = string.Empty; }

        [HttpPost("cancel")]
        [Authorize]
        public async Task<IActionResult> Cancel([FromBody] CancelInput input)
        {
            var me = await _userManager.GetUserAsync(User);
            if (me == null) return Unauthorized();

            // Try as user first; vendors can call vendor endpoint
            var isVendor = User.IsInRole(RoleTypes.Vendor) || User.IsInRole(RoleTypes.ServiceProvider);
            var ok = await _orders.CancelAsync(input.OrderId, me.Id, isVendor);
            return ok ? Ok() : BadRequest();
        }
    }
}