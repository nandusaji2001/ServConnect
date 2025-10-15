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

        public class CreateOrderWithAddressInput
        {
            public string ItemId { get; set; } = string.Empty;
            public int Quantity { get; set; }
            public string? UserAddressId { get; set; } // If using saved address
            public string FullName { get; set; } = string.Empty;
            public string PhoneNumber { get; set; } = string.Empty;
            public string AddressLine1 { get; set; } = string.Empty;
            public string? AddressLine2 { get; set; }
            public string City { get; set; } = string.Empty;
            public string State { get; set; } = string.Empty;
            public string PostalCode { get; set; } = string.Empty;
            public string Country { get; set; } = "India";
            public string? Landmark { get; set; }
        }

        [HttpPost]
        [Authorize]
        [ServiceFilter(typeof(ServConnect.Filters.RequireApprovedUserApiFilter))]
        public async Task<IActionResult> Create([FromBody] CreateOrderInput input)
        {
            if (string.IsNullOrWhiteSpace(input.ItemId) || input.Quantity <= 0)
                return BadRequest("Invalid item or quantity");

            var me = await _userManager.GetUserAsync(User);
            if (me == null) return Unauthorized();

            var item = await _items.GetByIdAsync(input.ItemId);
            if (item == null) return NotFound("Item not found");

            var (order, razorpayOrderId) = await _orders.CreateOrderAsync(me.Id, me.Email ?? string.Empty, input.ItemId, input.Quantity, input.ShippingAddress);
            
            // Return the data structure expected by the frontend
            var response = new
            {
                orderId = order.Id,
                razorpayOrderId = razorpayOrderId,
                key = _config["Razorpay:KeyId"],
                amount = (long)(order.TotalAmount * 100), // Convert to paise
                currency = "INR",
                description = $"Order for {order.ItemTitle} x{order.Quantity}"
            };
            
            return Ok(response);
        }

        [HttpPost("with-address")]
        [Authorize]
        [ServiceFilter(typeof(ServConnect.Filters.RequireApprovedUserApiFilter))]
        public async Task<IActionResult> CreateWithAddress([FromBody] CreateOrderWithAddressInput input)
        {
            if (string.IsNullOrWhiteSpace(input.ItemId) || input.Quantity <= 0)
                return BadRequest("Invalid item or quantity");

            var me = await _userManager.GetUserAsync(User);
            if (me == null) return Unauthorized();

            var address = new UserAddress
            {
                FullName = input.FullName,
                PhoneNumber = input.PhoneNumber,
                AddressLine1 = input.AddressLine1,
                AddressLine2 = input.AddressLine2,
                City = input.City,
                State = input.State,
                PostalCode = input.PostalCode,
                Country = input.Country,
                Landmark = input.Landmark
            };

            var (order, razorpayOrderId) = await _orders.CreateOrderWithAddressAsync(me.Id, me.Email ?? string.Empty, input.ItemId, input.Quantity, address, input.UserAddressId);
            
            // Return the data structure expected by the frontend
            var response = new
            {
                orderId = order.Id,
                razorpayOrderId = razorpayOrderId,
                key = _config["Razorpay:KeyId"],
                amount = (long)(order.TotalAmount * 100), // Convert to paise
                currency = "INR",
                description = $"Order for {order.ItemTitle} x{order.Quantity}"
            };
            
            return Ok(response);
        }

        [HttpGet("mine")]
        [Authorize]
        public async Task<IActionResult> GetMine()
        {
            var me = await _userManager.GetUserAsync(User);
            if (me == null) return Unauthorized();
            var list = await _orders.GetOrdersForUserAsync(me.Id);
            return Ok(list);
        }

        [HttpGet("vendor")]
        [Authorize(Roles = $"{RoleTypes.Vendor},{RoleTypes.ServiceProvider}")]
        [ServiceFilter(typeof(ServConnect.Filters.RequireApprovedUserApiFilter))]
        public async Task<IActionResult> VendorOrders()
        {
            var me = await _userManager.GetUserAsync(User);
            if (me == null) return Unauthorized();
            var list = await _orders.GetOrdersForVendorAsync(me.Id);
            return Ok(list);
        }

        public class ShipInput { public string OrderId { get; set; } = string.Empty; public string? TrackingUrl { get; set; } }
        public class UpdateStatusInput { public string OrderId { get; set; } = string.Empty; public string Status { get; set; } = string.Empty; public string? TrackingUrl { get; set; } }

        [HttpPost("ship")]
        [Authorize(Roles = $"{RoleTypes.Vendor},{RoleTypes.ServiceProvider}")]
        [ServiceFilter(typeof(ServConnect.Filters.RequireApprovedUserApiFilter))]
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

        [HttpPost("update-status")]
        [Authorize(Roles = $"{RoleTypes.Vendor},{RoleTypes.ServiceProvider}")]
        [ServiceFilter(typeof(ServConnect.Filters.RequireApprovedUserApiFilter))]
        public async Task<IActionResult> UpdateStatus([FromBody] UpdateStatusInput input)
        {
            var me = await _userManager.GetUserAsync(User);
            if (me == null) return Unauthorized();

            if (!Enum.TryParse<OrderStatus>(input.Status, out var status))
                return BadRequest("Invalid status");

            var ok = await _orders.UpdateOrderStatusAsync(input.OrderId, me.Id, status, input.TrackingUrl);
            return ok ? Ok() : BadRequest();
        }

        [HttpPost("accept")]
        [Authorize(Roles = $"{RoleTypes.Vendor},{RoleTypes.ServiceProvider}")]
        [ServiceFilter(typeof(ServConnect.Filters.RequireApprovedUserApiFilter))]
        public async Task<IActionResult> AcceptOrder([FromBody] DeliverInput input)
        {
            var me = await _userManager.GetUserAsync(User);
            if (me == null) return Unauthorized();
            var ok = await _orders.AcceptOrderAsync(input.OrderId, me.Id);
            return ok ? Ok() : BadRequest();
        }

        [HttpPost("mark-packed")]
        [Authorize(Roles = $"{RoleTypes.Vendor},{RoleTypes.ServiceProvider}")]
        [ServiceFilter(typeof(ServConnect.Filters.RequireApprovedUserApiFilter))]
        public async Task<IActionResult> MarkPacked([FromBody] DeliverInput input)
        {
            var me = await _userManager.GetUserAsync(User);
            if (me == null) return Unauthorized();
            var ok = await _orders.MarkPackedAsync(input.OrderId, me.Id);
            return ok ? Ok() : BadRequest();
        }

        [HttpPost("mark-out-for-delivery")]
        [Authorize(Roles = $"{RoleTypes.Vendor},{RoleTypes.ServiceProvider}")]
        [ServiceFilter(typeof(ServConnect.Filters.RequireApprovedUserApiFilter))]
        public async Task<IActionResult> MarkOutForDelivery([FromBody] DeliverInput input)
        {
            var me = await _userManager.GetUserAsync(User);
            if (me == null) return Unauthorized();
            var ok = await _orders.MarkOutForDeliveryAsync(input.OrderId, me.Id);
            return ok ? Ok() : BadRequest();
        }

        [HttpPost("verify-payment")]
        [Authorize]
        public async Task<IActionResult> VerifyPayment([FromBody] VerifyPaymentInput input)
        {
            var ok = await _orders.VerifyPaymentAndConfirmAsync(input.OrderId, input.RazorpayOrderId, input.RazorpayPaymentId, input.RazorpaySignature);
            return ok ? Ok() : BadRequest("Payment verification failed");
        }
    }

    public class VerifyPaymentInput
    {
        public string OrderId { get; set; } = string.Empty;
        public string RazorpayOrderId { get; set; } = string.Empty;
        public string RazorpayPaymentId { get; set; } = string.Empty;
        public string RazorpaySignature { get; set; } = string.Empty;
    }
}