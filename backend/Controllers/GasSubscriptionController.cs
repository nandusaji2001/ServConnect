using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using ServConnect.Models;
using ServConnect.Services;

namespace ServConnect.Controllers
{
    /// <summary>
    /// Controller for Gas Subscription module - IoT-based automatic gas cylinder booking
    /// </summary>
    [Authorize]
    [ServiceFilter(typeof(ServConnect.Filters.RequireApprovedUserFilter))]
    public class GasSubscriptionController : Controller
    {
        private readonly IGasSubscriptionService _gasService;
        private readonly UserManager<Users> _userManager;
        private readonly ILogger<GasSubscriptionController> _logger;

        public GasSubscriptionController(
            IGasSubscriptionService gasService,
            UserManager<Users> userManager,
            ILogger<GasSubscriptionController> logger)
        {
            _gasService = gasService;
            _userManager = userManager;
            _logger = logger;
        }

        /// <summary>
        /// Main dashboard for gas subscription
        /// </summary>
        public async Task<IActionResult> Index()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return RedirectToAction("Login", "Account");

            var dashboard = await _gasService.GetUserDashboardAsync(user.Id);
            var vendors = await _gasService.GetGasVendorsAsync();

            ViewBag.Vendors = vendors;
            ViewBag.UserName = user.FullName;
            ViewBag.UserPhone = user.PhoneNumber ?? string.Empty;
            ViewBag.UserAddress = user.Address ?? string.Empty;

            return View(dashboard);
        }

        /// <summary>
        /// Settings page for gas subscription
        /// </summary>
        public async Task<IActionResult> Settings()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return RedirectToAction("Login", "Account");

            var subscription = await _gasService.GetSubscriptionByUserIdAsync(user.Id);
            var vendors = await _gasService.GetGasVendorsAsync();

            ViewBag.Vendors = vendors;
            ViewBag.UserPhone = user.PhoneNumber ?? string.Empty;
            ViewBag.UserAddress = user.Address ?? string.Empty;

            return View(subscription);
        }

        /// <summary>
        /// Order history page
        /// </summary>
        public async Task<IActionResult> Orders()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return RedirectToAction("Login", "Account");

            var orders = await _gasService.GetUserOrdersAsync(user.Id, 50);
            return View(orders);
        }

        /// <summary>
        /// Order details page
        /// </summary>
        public async Task<IActionResult> OrderDetails(string id)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return RedirectToAction("Login", "Account");

            var order = await _gasService.GetOrderByIdAsync(id);
            if (order == null || order.UserId != user.Id)
                return NotFound();

            return View(order);
        }

        /// <summary>
        /// Manual order page
        /// </summary>
        public async Task<IActionResult> PlaceOrder()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return RedirectToAction("Login", "Account");

            var vendors = await _gasService.GetGasVendorsAsync();
            var subscription = await _gasService.GetSubscriptionByUserIdAsync(user.Id);

            ViewBag.Vendors = vendors;
            ViewBag.Subscription = subscription;

            return View();
        }
    }

    /// <summary>
    /// API Controller for Gas Subscription - handles IoT readings and AJAX requests
    /// </summary>
    [ApiController]
    [Route("api/[controller]")]
    public class GasSubscriptionApiController : ControllerBase
    {
        private readonly IGasSubscriptionService _gasService;
        private readonly UserManager<Users> _userManager;
        private readonly ILogger<GasSubscriptionApiController> _logger;

        public GasSubscriptionApiController(
            IGasSubscriptionService gasService,
            UserManager<Users> userManager,
            ILogger<GasSubscriptionApiController> logger)
        {
            _gasService = gasService;
            _userManager = userManager;
            _logger = logger;
        }

        #region IoT Endpoints (for ESP32)

        /// <summary>
        /// Receive gas weight reading from ESP32
        /// This is the main endpoint for IoT device communication
        /// </summary>
        [HttpPost("reading")]
        [AllowAnonymous] // Allow ESP32 to post without auth
        public async Task<IActionResult> PostReading([FromBody] GasReadingRequest request)
        {
            try
            {
                _logger.LogInformation("=== GAS READING RECEIVED ===");
                _logger.LogInformation("Weight: {Weight}kg, DeviceId: {DeviceId}", request.Weight, request.DeviceId);

                var reading = await _gasService.ProcessGasReadingAsync(request);

                return Ok(new
                {
                    success = true,
                    message = "Reading processed",
                    data = new
                    {
                        weightGrams = reading.WeightGrams,
                        gasPercentage = reading.GasPercentage,
                        status = reading.Status,
                        timestamp = reading.Timestamp
                    }
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing gas reading");
                return BadRequest(new { success = false, message = ex.Message });
            }
        }

        /// <summary>
        /// Simple weight endpoint (compatible with existing GasMonitorDashboard)
        /// </summary>
        [HttpPost("weight/simple")]
        [AllowAnonymous]
        public async Task<IActionResult> PostSimpleWeight([FromBody] GasReadingRequest request)
        {
            return await PostReading(request);
        }

        #endregion

        #region User Endpoints

        /// <summary>
        /// Get current gas subscription settings
        /// </summary>
        [HttpGet("subscription")]
        [Authorize]
        [ServiceFilter(typeof(ServConnect.Filters.RequireApprovedUserApiFilter))]
        public async Task<IActionResult> GetSubscription()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return Unauthorized();

            var subscription = await _gasService.GetSubscriptionByUserIdAsync(user.Id);
            return Ok(subscription);
        }

        /// <summary>
        /// Create or update gas subscription settings
        /// </summary>
        [HttpPost("subscription")]
        [Authorize]
        [ServiceFilter(typeof(ServConnect.Filters.RequireApprovedUserApiFilter))]
        public async Task<IActionResult> SaveSubscription([FromBody] GasSubscriptionRequest request)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return Unauthorized();

            try
            {
                var subscription = await _gasService.CreateOrUpdateSubscriptionAsync(user.Id, request);
                return Ok(new { success = true, subscription });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error saving subscription");
                return BadRequest(new { success = false, message = ex.Message });
            }
        }

        /// <summary>
        /// Get dashboard data
        /// </summary>
        [HttpGet("dashboard")]
        [Authorize]
        [ServiceFilter(typeof(ServConnect.Filters.RequireApprovedUserApiFilter))]
        public async Task<IActionResult> GetDashboard()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return Unauthorized();

            var dashboard = await _gasService.GetUserDashboardAsync(user.Id);
            return Ok(dashboard);
        }

        /// <summary>
        /// Get recent gas readings
        /// </summary>
        [HttpGet("readings")]
        [Authorize]
        [ServiceFilter(typeof(ServConnect.Filters.RequireApprovedUserApiFilter))]
        public async Task<IActionResult> GetReadings([FromQuery] int count = 50)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return Unauthorized();

            var readings = await _gasService.GetRecentReadingsAsync(user.Id, count);
            return Ok(readings);
        }

        /// <summary>
        /// Get latest gas reading
        /// </summary>
        [HttpGet("readings/latest")]
        [Authorize]
        [ServiceFilter(typeof(ServConnect.Filters.RequireApprovedUserApiFilter))]
        public async Task<IActionResult> GetLatestReading()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return Unauthorized();

            var reading = await _gasService.GetLatestReadingAsync(user.Id);
            return Ok(reading);
        }

        /// <summary>
        /// Get available gas vendors
        /// </summary>
        [HttpGet("vendors")]
        [Authorize]
        [ServiceFilter(typeof(ServConnect.Filters.RequireApprovedUserApiFilter))]
        public async Task<IActionResult> GetVendors()
        {
            var vendors = await _gasService.GetGasVendorsAsync();
            var vendorList = vendors.Select(v => new
            {
                id = v.Id.ToString(),
                name = v.BusinessName ?? v.FullName,
                address = v.BusinessAddress ?? v.Address
            });
            return Ok(vendorList);
        }

        /// <summary>
        /// Place a manual gas order
        /// </summary>
        [HttpPost("orders")]
        [Authorize]
        [ServiceFilter(typeof(ServConnect.Filters.RequireApprovedUserApiFilter))]
        public async Task<IActionResult> PlaceOrder([FromBody] PlaceGasOrderRequest request)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return Unauthorized();

            try
            {
                if (!Guid.TryParse(request.VendorId, out var vendorGuid))
                    return BadRequest(new { success = false, message = "Invalid vendor ID" });

                var order = await _gasService.CreateGasOrderAsync(
                    user.Id,
                    vendorGuid,
                    isAutoTriggered: false);

                return Ok(new { success = true, order });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error placing order");
                return BadRequest(new { success = false, message = ex.Message });
            }
        }

        /// <summary>
        /// Get user's order history
        /// </summary>
        [HttpGet("orders")]
        [Authorize]
        [ServiceFilter(typeof(ServConnect.Filters.RequireApprovedUserApiFilter))]
        public async Task<IActionResult> GetOrders([FromQuery] int limit = 20)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return Unauthorized();

            var orders = await _gasService.GetUserOrdersAsync(user.Id, limit);
            return Ok(orders);
        }

        /// <summary>
        /// Get a specific order
        /// </summary>
        [HttpGet("orders/{id}")]
        [Authorize]
        [ServiceFilter(typeof(ServConnect.Filters.RequireApprovedUserApiFilter))]
        public async Task<IActionResult> GetOrder(string id)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return Unauthorized();

            var order = await _gasService.GetOrderByIdAsync(id);
            if (order == null || order.UserId != user.Id)
                return NotFound();

            return Ok(order);
        }

        #endregion

        #region Vendor Endpoints

        /// <summary>
        /// Get vendor's gas orders
        /// </summary>
        [HttpGet("vendor/orders")]
        [Authorize(Roles = RoleTypes.Vendor)]
        [ServiceFilter(typeof(ServConnect.Filters.RequireApprovedUserApiFilter))]
        public async Task<IActionResult> GetVendorOrders([FromQuery] int limit = 50)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return Unauthorized();

            var orders = await _gasService.GetVendorOrdersAsync(user.Id, limit);
            return Ok(orders);
        }

        /// <summary>
        /// Get vendor's pending orders
        /// </summary>
        [HttpGet("vendor/orders/pending")]
        [Authorize(Roles = RoleTypes.Vendor)]
        [ServiceFilter(typeof(ServConnect.Filters.RequireApprovedUserApiFilter))]
        public async Task<IActionResult> GetVendorPendingOrders()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return Unauthorized();

            var orders = await _gasService.GetPendingVendorOrdersAsync(user.Id);
            return Ok(orders);
        }

        /// <summary>
        /// Update order status (for vendors)
        /// </summary>
        [HttpPut("vendor/orders/{id}/status")]
        [Authorize(Roles = RoleTypes.Vendor)]
        [ServiceFilter(typeof(ServConnect.Filters.RequireApprovedUserApiFilter))]
        public async Task<IActionResult> UpdateOrderStatus(string id, [FromBody] UpdateGasOrderStatusRequest request)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return Unauthorized();

            var order = await _gasService.GetOrderByIdAsync(id);
            if (order == null)
                return NotFound();

            if (order.VendorId != user.Id)
                return Forbid();

            try
            {
                var updated = await _gasService.UpdateOrderStatusAsync(id, request.Status, request.Message);
                return Ok(new { success = true, order = updated });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating order status");
                return BadRequest(new { success = false, message = ex.Message });
            }
        }

        #endregion
    }

    #region Request Models

    public class PlaceGasOrderRequest
    {
        public string VendorId { get; set; } = string.Empty;
    }

    public class UpdateGasOrderStatusRequest
    {
        public GasOrderStatus Status { get; set; }
        public string? Message { get; set; }
    }

    #endregion
}
