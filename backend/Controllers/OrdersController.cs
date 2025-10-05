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
        [ServiceFilter(typeof(ServConnect.Filters.RequireApprovedUserApiFilter))]
        public async Task<IActionResult> Create([FromBody] CreateOrderInput input)
        {
            if (string.IsNullOrWhiteSpace(input.ItemId) || input.Quantity <= 0)
                return BadRequest("Invalid item or quantity");

            var me = await _userManager.GetUserAsync(User);
            if (me == null) return Unauthorized();

            var item = await _items.GetByIdAsync(input.ItemId);
            if (item == null) return NotFound("Item not found");

            var order = await _orders.CreateOrderAsync(me.Id, me.Email ?? string.Empty, input.ItemId, input.Quantity, input.ShippingAddress);
            return Ok(order);
        }

        [HttpGet]
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
    }
}