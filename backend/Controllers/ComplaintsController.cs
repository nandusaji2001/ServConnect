using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using ServConnect.Models;
using ServConnect.Services;
using ServConnect.ViewModels;

namespace ServConnect.Controllers
{
    [Authorize]
    public class ComplaintsController : Controller
    {
        private readonly IComplaintService _complaintService;
        private readonly IBookingService _bookingService;
        private readonly IOrderService _orderService;
        private readonly UserManager<Users> _userManager;
        private readonly IWebHostEnvironment _env;

        public ComplaintsController(
            IComplaintService complaintService,
            IBookingService bookingService,
            IOrderService orderService,
            UserManager<Users> userManager,
            IWebHostEnvironment env)
        {
            _complaintService = complaintService;
            _bookingService = bookingService;
            _orderService = orderService;
            _userManager = userManager;
            _env = env;
        }

        // User complaint form
        [HttpGet]
        public async Task<IActionResult> Create()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return Challenge();

            var roles = await _userManager.GetRolesAsync(user);
            var effectiveRole = roles.FirstOrDefault() ?? RoleTypes.User;

            // Get completed bookings for this user
            var bookings = await _bookingService.GetForUserAsync(user.Id);
            var completedBookings = bookings
                .Where(b => b.IsCompleted || b.ServiceStatus == ServiceStatus.Completed)
                .OrderByDescending(b => b.CompletedAtUtc ?? b.ServiceCompletedAt)
                .Take(20)
                .Select(b => new UserBookingOption
                {
                    BookingId = b.Id ?? "",
                    ServiceName = b.ServiceName,
                    ProviderName = b.ProviderName,
                    ProviderId = b.ProviderId,
                    CompletedAt = b.CompletedAtUtc ?? b.ServiceCompletedAt ?? DateTime.UtcNow
                })
                .ToList();

            // Get delivered orders for this user
            var orders = await _orderService.GetOrdersForUserAsync(user.Id);
            var deliveredOrders = orders
                .Where(o => o.Status == OrderStatus.Delivered)
                .OrderByDescending(o => o.UpdatedAtUtc)
                .Take(20)
                .Select(o => new UserOrderOption
                {
                    OrderId = o.Id ?? "",
                    ItemName = o.ItemTitle,
                    VendorId = o.VendorId,
                    VendorName = "", // Will be populated via API if needed
                    DeliveredAt = o.UpdatedAtUtc
                })
                .ToList();

            var vm = new ComplaintFormDataViewModel
            {
                Form = new ComplaintCreateViewModel
                {
                    Name = user.FullName ?? string.Empty,
                    Email = user.Email ?? string.Empty,
                    ComplainantId = user.Id,
                    Role = effectiveRole,
                    IsElderly = user.IsElder
                },
                CompletedBookings = completedBookings,
                DeliveredOrders = deliveredOrders
            };

            return View("~/Views/Complaints/Create.cshtml", vm);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create([Bind(Prefix = "Form")] ComplaintCreateViewModel vm)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return Challenge();

            // Set the complainant info from the current user (in case form was tampered)
            vm.ComplainantId = user.Id;
            vm.Name = user.FullName ?? string.Empty;
            vm.Email = user.Email ?? string.Empty;
            vm.IsElderly = user.IsElder;

            if (!ModelState.IsValid)
            {
                var bookings = await _bookingService.GetForUserAsync(user.Id);
                var orders = await _orderService.GetOrdersForUserAsync(user.Id);
                var roles = await _userManager.GetRolesAsync(user);
                vm.Role = roles.FirstOrDefault() ?? RoleTypes.User;
                
                var formData = new ComplaintFormDataViewModel
                {
                    Form = vm,
                    CompletedBookings = bookings
                        .Where(b => b.IsCompleted || b.ServiceStatus == ServiceStatus.Completed)
                        .Select(b => new UserBookingOption
                        {
                            BookingId = b.Id ?? "",
                            ServiceName = b.ServiceName,
                            ProviderName = b.ProviderName,
                            ProviderId = b.ProviderId,
                            CompletedAt = b.CompletedAtUtc ?? b.ServiceCompletedAt ?? DateTime.UtcNow
                        }).ToList(),
                    DeliveredOrders = orders
                        .Where(o => o.Status == OrderStatus.Delivered)
                        .Select(o => new UserOrderOption
                        {
                            OrderId = o.Id ?? "",
                            ItemName = o.ItemTitle,
                            VendorId = o.VendorId,
                            DeliveredAt = o.UpdatedAtUtc
                        }).ToList()
                };
                return View("~/Views/Complaints/Create.cshtml", formData);
            }

            var complaint = new Complaint
            {
                ComplainantId = user.Id,
                ComplainantName = user.FullName ?? vm.Name,
                ComplainantEmail = user.Email ?? vm.Email,
                ComplainantPhone = vm.Phone,
                ComplainantRole = vm.Role,
                IsElderly = user.IsElder,
                Category = vm.Category,
                SubCategory = vm.SubCategory ?? string.Empty,
                ServiceProviderId = vm.ServiceProviderId,
                ServiceProviderName = vm.ServiceProviderName,
                VendorId = vm.VendorId,
                VendorName = vm.VendorName,
                BookingId = vm.BookingId,
                BookingServiceName = vm.BookingServiceName,
                OrderId = vm.OrderId,
                OrderItemName = vm.OrderItemName,
                Description = vm.Description
            };

            await _complaintService.CreateAsync(complaint);

            // Handle evidence upload
            if (vm.EvidenceFiles?.Count > 0)
            {
                var baseFolder = Path.Combine(_env.WebRootPath, "uploads", "complaints", complaint.Id);
                if (!Directory.Exists(baseFolder)) Directory.CreateDirectory(baseFolder);

                foreach (var file in vm.EvidenceFiles)
                {
                    if (file.Length <= 0) continue;
                    var safeName = Path.GetFileName(file.FileName);
                    var savePath = Path.Combine(baseFolder, safeName);
                    using (var fs = System.IO.File.Create(savePath))
                    {
                        await file.CopyToAsync(fs);
                    }
                    var relUrl = $"/uploads/complaints/{complaint.Id}/{safeName}";
                    await _complaintService.AddEvidenceAsync(complaint.Id, relUrl);
                }
            }

            TempData["ComplaintMessage"] = "Your complaint has been submitted successfully. You can track its status from your dashboard.";
            return RedirectToAction(nameof(MyComplaints));
        }

        // User's complaint list with status tracking
        [HttpGet]
        public async Task<IActionResult> MyComplaints()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return Challenge();

            var complaints = await _complaintService.GetByComplainantAsync(user.Id);
            return View("~/Views/Complaints/MyComplaints.cshtml", complaints);
        }

        // User view complaint details
        [HttpGet]
        public async Task<IActionResult> Details(string id)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return Challenge();

            var complaint = await _complaintService.GetByIdAsync(id);
            if (complaint == null) return NotFound();

            // Only allow viewing own complaints (unless admin)
            var roles = await _userManager.GetRolesAsync(user);
            if (complaint.ComplainantId != user.Id && !roles.Contains(RoleTypes.Admin))
            {
                return Forbid();
            }

            return View("~/Views/Complaints/Details.cshtml", complaint);
        }

        // API to get booking details for complaint form
        [HttpGet]
        [Route("api/complaints/booking/{bookingId}")]
        public async Task<IActionResult> GetBookingDetails(string bookingId)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return Unauthorized();

            var booking = await _bookingService.GetByIdAsync(bookingId);
            if (booking == null || booking.UserId != user.Id)
                return NotFound();

            return Json(new
            {
                bookingId = booking.Id,
                serviceName = booking.ServiceName,
                providerName = booking.ProviderName,
                providerId = booking.ProviderId,
                completedAt = booking.CompletedAtUtc ?? booking.ServiceCompletedAt
            });
        }

        // API to get order details for complaint form
        [HttpGet]
        [Route("api/complaints/order/{orderId}")]
        public async Task<IActionResult> GetOrderDetails(string orderId)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return Unauthorized();

            var order = await _orderService.GetByIdAsync(orderId);
            if (order == null || order.UserId != user.Id)
                return NotFound();

            return Json(new
            {
                orderId = order.Id,
                itemName = order.ItemTitle,
                vendorId = order.VendorId,
                deliveredAt = order.UpdatedAtUtc
            });
        }
    }
}
