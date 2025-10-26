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
        private readonly IBookingPaymentService _paymentService;
        private readonly IAvailabilityValidationService _availabilityValidation;
        private readonly IServiceOtpService _serviceOtpService;
        private readonly IServiceTransferService _transferService;

        public BookingsController(IBookingService bookings, UserManager<Users> userManager, IServiceCatalog catalog, IBookingPaymentService paymentService, IAvailabilityValidationService availabilityValidation, IServiceOtpService serviceOtpService, IServiceTransferService transferService)
        {
            _bookings = bookings;
            _userManager = userManager;
            _catalog = catalog;
            _paymentService = paymentService;
            _availabilityValidation = availabilityValidation;
            _serviceOtpService = serviceOtpService;
            _transferService = transferService;
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

        // Test endpoint to verify API is working
        [HttpGet("/api/bookings/test")]
        public IActionResult Test()
        {
            return Ok(new { message = "Bookings API is working", timestamp = DateTime.UtcNow });
        }

        [HttpPost("/api/bookings")] 
        [Authorize] // any logged-in user can request a booking
        [ServiceFilter(typeof(ServConnect.Filters.RequireApprovedUserApiFilter))]
        public async Task<IActionResult> Create([FromBody] CreateBookingRequest req)
        {
            Console.WriteLine($"[DEBUG] Booking API called at {DateTime.Now}");
            try
            {
                if (string.IsNullOrWhiteSpace(req.ProviderServiceId)) return BadRequest("Provider service is required");
                
                // Get the provider service to validate availability
                var providerService = await _catalog.GetProviderServiceByIdAsync(req.ProviderServiceId);
                if (providerService == null) return BadRequest("Service not found");
                
                // Validate availability using the new service
                var availabilityResult = await _availabilityValidation.ValidateBookingAvailabilityAsync(providerService, req.ServiceDateTime);
                if (!availabilityResult.IsValid)
                {
                    return BadRequest(new { 
                        error = availabilityResult.ErrorMessage,
                        availableDays = availabilityResult.AvailableDays,
                        availableHours = availabilityResult.AvailableHours
                    });
                }

            var me = await _userManager.GetUserAsync(User);
            if (me == null) return Unauthorized();

            // Check for pending payments - user must complete all pending payments before booking new services
            var hasPendingPayments = await _paymentService.HasPendingPaymentsAsync(me.Id);
            if (hasPendingPayments)
            {
                var pendingPayments = await _paymentService.GetPendingPaymentsByUserAsync(me.Id);
                return BadRequest(new { 
                    error = "Please complete your pending payments before booking new services.",
                    pendingPayments = pendingPayments.Select(p => new {
                        id = p.Id,
                        serviceName = p.ServiceName,
                        providerName = p.ProviderName,
                        amount = p.AmountInRupees,
                        paymentUrl = $"/booking-payment/pay/{p.Id}"
                    })
                });
            }

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
            catch (Exception ex)
            {
                Console.WriteLine($"[ERROR] Booking creation failed: {ex.Message}");
                Console.WriteLine($"[ERROR] Stack trace: {ex.StackTrace}");
                return StatusCode(500, new { error = "Internal server error", message = ex.Message });
            }
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
        [ServiceFilter(typeof(ServConnect.Filters.RequireApprovedUserApiFilter))]
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
        [ServiceFilter(typeof(ServConnect.Filters.RequireApprovedUserApiFilter))]
        public async Task<IActionResult> Complete([FromBody] CompleteRequest req)
        {
            var me = await _userManager.GetUserAsync(User);
            if (me == null) return Unauthorized();
            
            // Get the booking to extract service details
            var userBookings = await _bookings.GetForUserAsync(me.Id);
            var booking = userBookings.FirstOrDefault(b => b.Id == req.BookingId);
            if (booking == null) return NotFound();

            // Instead of completing directly, redirect to payment
            // The payment amount should be the service price
            var paymentAmount = booking.Price > 0 ? booking.Price : 100; // Default ₹100 if no price set

            return Ok(new { 
                requiresPayment = true, 
                bookingId = req.BookingId,
                amount = paymentAmount,
                serviceName = booking.ServiceName,
                providerName = booking.ProviderName,
                rating = req.Rating,
                feedback = req.Feedback
            });
        }

        // User view: my bookings
        [HttpGet("/my/bookings")] 
        [Authorize]
        public async Task<IActionResult> MyBookings()
        {
            var me = await _userManager.GetUserAsync(User);
            if (me == null) return Unauthorized();
            var list = await _bookings.GetForUserAsync(me.Id);
            
            // Get pending payments to show notifications
            var pendingPayments = await _paymentService.GetPendingPaymentsByUserAsync(me.Id);
            ViewBag.PendingPayments = pendingPayments;
            ViewBag.HasPendingPayments = pendingPayments.Any();
            
            return View("UserBookings", list);
        }

        // API endpoint to get availability information for a service
        [HttpGet("/api/bookings/availability/{providerServiceId}")]
        public async Task<IActionResult> GetAvailability(string providerServiceId, [FromQuery] DateTime? date = null)
        {
            try
            {
                var providerService = await _catalog.GetProviderServiceByIdAsync(providerServiceId);
                if (providerService == null) return NotFound("Service not found");

                var targetDate = date ?? DateTime.Today;
                
                var result = new
                {
                    availableDays = providerService.AvailableDays,
                    availableHours = providerService.AvailableHours,
                    isDayAvailable = _availabilityValidation.IsDayAvailable(providerService, targetDate.DayOfWeek),
                    timeSlots = await _availabilityValidation.GetAvailableTimeSlotsAsync(providerService, targetDate)
                };

                return Ok(result);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ERROR] Get availability failed: {ex.Message}");
                return StatusCode(500, new { error = "Internal server error" });
            }
        }

        // API endpoint to validate a specific date/time
        [HttpPost("/api/bookings/validate-availability")]
        public async Task<IActionResult> ValidateAvailability([FromBody] ValidateAvailabilityRequest req)
        {
            try
            {
                var providerService = await _catalog.GetProviderServiceByIdAsync(req.ProviderServiceId);
                if (providerService == null) return NotFound("Service not found");

                var result = await _availabilityValidation.ValidateBookingAvailabilityAsync(providerService, req.RequestedDateTime);
                
                return Ok(new
                {
                    isValid = result.IsValid,
                    errorMessage = result.ErrorMessage,
                    availableDays = result.AvailableDays,
                    availableHours = result.AvailableHours
                });
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ERROR] Validate availability failed: {ex.Message}");
                return StatusCode(500, new { error = "Internal server error" });
            }
        }

        // API endpoint to start a service (generates OTP for user)
        [HttpPost("/api/bookings/{bookingId}/start-service")]
        [Authorize(Roles = RoleTypes.ServiceProvider)]
        [ServiceFilter(typeof(ServConnect.Filters.RequireApprovedUserApiFilter))]
        public async Task<IActionResult> StartService(string bookingId)
        {
            try
            {
                var me = await _userManager.GetUserAsync(User);
                if (me == null) return Unauthorized();

                // Get the booking and verify provider ownership
                var providerBookings = await _bookings.GetForProviderAsync(me.Id);
                var booking = providerBookings.FirstOrDefault(b => b.Id == bookingId);
                
                if (booking == null) return NotFound("Booking not found");
                if (booking.Status != BookingStatus.Accepted) return BadRequest("Booking must be accepted first");
                if (booking.ServiceStatus != ServiceStatus.NotStarted) return BadRequest("Service already started or completed");

                // Generate OTP for the user
                var otp = await _serviceOtpService.GenerateOtpAsync(
                    bookingId, 
                    booking.UserId, 
                    me.Id, 
                    booking.ServiceName, 
                    booking.ProviderName
                );

                // Update booking with OTP reference
                await _bookings.UpdateServiceOtpAsync(bookingId, otp.Id!);

                return Ok(new { 
                    message = "OTP sent to customer. Please ask customer for the 6-digit code to start service.",
                    otpId = otp.Id,
                    expiresAt = otp.ExpiresAt
                });
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ERROR] Start service failed: {ex.Message}");
                return StatusCode(500, new { error = "Internal server error" });
            }
        }

        // API endpoint to verify OTP and actually start the service
        [HttpPost("/api/bookings/{bookingId}/verify-start")]
        [Authorize(Roles = RoleTypes.ServiceProvider)]
        [ServiceFilter(typeof(ServConnect.Filters.RequireApprovedUserApiFilter))]
        public async Task<IActionResult> VerifyAndStartService(string bookingId, [FromBody] VerifyOtpRequest req)
        {
            try
            {
                var me = await _userManager.GetUserAsync(User);
                if (me == null) return Unauthorized();

                // Validate OTP
                var isValidOtp = await _serviceOtpService.ValidateOtpAsync(bookingId, req.OtpCode, me.Id);
                if (!isValidOtp)
                {
                    return BadRequest(new { error = "Invalid or expired OTP code" });
                }

                // Update booking status to InProgress
                var success = await _bookings.UpdateServiceStatusAsync(bookingId, me.Id, ServiceStatus.InProgress);
                if (!success)
                {
                    return BadRequest(new { error = "Failed to start service" });
                }

                return Ok(new { message = "Service started successfully!" });
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ERROR] Verify and start service failed: {ex.Message}");
                return StatusCode(500, new { error = "Internal server error" });
            }
        }

        // API endpoint to stop/complete a service
        [HttpPost("/api/bookings/{bookingId}/stop-service")]
        [Authorize(Roles = RoleTypes.ServiceProvider)]
        [ServiceFilter(typeof(ServConnect.Filters.RequireApprovedUserApiFilter))]
        public async Task<IActionResult> StopService(string bookingId)
        {
            try
            {
                var me = await _userManager.GetUserAsync(User);
                if (me == null) return Unauthorized();

                // Get the booking and verify provider ownership
                var providerBookings = await _bookings.GetForProviderAsync(me.Id);
                var booking = providerBookings.FirstOrDefault(b => b.Id == bookingId);
                
                if (booking == null) return NotFound("Booking not found");
                if (booking.ServiceStatus != ServiceStatus.InProgress) return BadRequest("Service is not currently in progress");

                // Update booking status to Completed
                var success = await _bookings.UpdateServiceStatusAsync(bookingId, me.Id, ServiceStatus.Completed);
                if (!success)
                {
                    return BadRequest(new { error = "Failed to stop service" });
                }

                return Ok(new { message = "Service completed successfully! Customer can now provide rating." });
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ERROR] Stop service failed: {ex.Message}");
                return StatusCode(500, new { error = "Internal server error" });
            }
        }

        // API endpoint to get active OTPs for a user (for notifications)
        [HttpGet("/api/bookings/active-otps")]
        [Authorize]
        public async Task<IActionResult> GetActiveOtps()
        {
            try
            {
                var me = await _userManager.GetUserAsync(User);
                if (me == null) return Unauthorized();

                var otps = await _serviceOtpService.GetActiveOtpsForUserAsync(me.Id);
                
                return Ok(otps.Select(otp => new {
                    id = otp.Id,
                    otpCode = otp.OtpCode,
                    serviceName = otp.ServiceName,
                    providerName = otp.ProviderName,
                    expiresAt = otp.ExpiresAt,
                    timeRemaining = (otp.ExpiresAt - DateTime.UtcNow).TotalMinutes
                }));
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ERROR] Get active OTPs failed: {ex.Message}");
                return StatusCode(500, new { error = "Internal server error" });
            }
        }

        public class VerifyOtpRequest
        {
            public string OtpCode { get; set; } = string.Empty;
        }

        public class ValidateAvailabilityRequest
        {
            public string ProviderServiceId { get; set; } = string.Empty;
            public DateTime RequestedDateTime { get; set; }
        }

        // Service Transfer API Endpoints

        public class CreateTransferRequestModel
        {
            public string BookingId { get; set; } = string.Empty;
            public Guid NewProviderId { get; set; }
            public string? TransferReason { get; set; }
        }

        public class TransferResponseRequest
        {
            public string TransferId { get; set; } = string.Empty;
            public string? Message { get; set; }
        }

        // Create a transfer request (Original provider)
        [HttpPost("/api/bookings/transfer/create")]
        [Authorize]
        [ServiceFilter(typeof(ServConnect.Filters.RequireApprovedUserApiFilter))]
        public async Task<IActionResult> CreateTransferRequest([FromBody] CreateTransferRequestModel req)
        {
            try
            {
                var me = await _userManager.GetUserAsync(User);
                if (me == null) return Unauthorized();

                var transfer = await _transferService.CreateTransferRequestAsync(
                    req.BookingId, 
                    me.Id, 
                    req.NewProviderId, 
                    req.TransferReason
                );

                return Ok(new { 
                    message = "Transfer request created successfully", 
                    transferId = transfer.Id 
                });
            }
            catch (ArgumentException ex)
            {
                return BadRequest(new { error = ex.Message });
            }
            catch (UnauthorizedAccessException ex)
            {
                return Forbid(ex.Message);
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(new { error = ex.Message });
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ERROR] Create transfer request failed: {ex.Message}");
                return StatusCode(500, new { error = "Internal server error" });
            }
        }

        // Get available providers for transfer
        [HttpGet("/api/bookings/transfer/available-providers/{providerServiceId}")]
        [Authorize]
        [ServiceFilter(typeof(ServConnect.Filters.RequireApprovedUserApiFilter))]
        public async Task<IActionResult> GetAvailableProvidersForTransfer(string providerServiceId)
        {
            try
            {
                var me = await _userManager.GetUserAsync(User);
                if (me == null) return Unauthorized();

                var providers = await _transferService.GetAvailableProvidersForTransferAsync(providerServiceId, me.Id);
                
                return Ok(providers.Select(p => new {
                    id = p.Id,
                    providerId = p.ProviderId,
                    providerName = p.ProviderName,
                    serviceName = p.ServiceName,
                    description = p.Description,
                    price = p.Price,
                    priceUnit = p.PriceUnit,
                    currency = p.Currency,
                    availableDays = p.AvailableDays,
                    availableHours = p.AvailableHours
                }));
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ERROR] Get available providers failed: {ex.Message}");
                Console.WriteLine($"[ERROR] Stack trace: {ex.StackTrace}");
                return StatusCode(500, new { error = "Internal server error", details = ex.Message });
            }
        }

        // User approve transfer
        [HttpPost("/api/bookings/transfer/user-approve")]
        [Authorize]
        [ServiceFilter(typeof(ServConnect.Filters.RequireApprovedUserApiFilter))]
        public async Task<IActionResult> UserApproveTransfer([FromBody] TransferResponseRequest req)
        {
            try
            {
                var me = await _userManager.GetUserAsync(User);
                if (me == null) return Unauthorized();

                var success = await _transferService.UserApproveTransferAsync(req.TransferId, me.Id, req.Message);
                if (!success)
                {
                    return BadRequest(new { error = "Failed to approve transfer request" });
                }

                return Ok(new { message = "Transfer request approved successfully" });
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ERROR] User approve transfer failed: {ex.Message}");
                return StatusCode(500, new { error = "Internal server error" });
            }
        }

        // User reject transfer
        [HttpPost("/api/bookings/transfer/user-reject")]
        [Authorize]
        [ServiceFilter(typeof(ServConnect.Filters.RequireApprovedUserApiFilter))]
        public async Task<IActionResult> UserRejectTransfer([FromBody] TransferResponseRequest req)
        {
            try
            {
                var me = await _userManager.GetUserAsync(User);
                if (me == null) return Unauthorized();

                var success = await _transferService.UserRejectTransferAsync(req.TransferId, me.Id, req.Message);
                if (!success)
                {
                    return BadRequest(new { error = "Failed to reject transfer request" });
                }

                return Ok(new { message = "Transfer request rejected successfully" });
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ERROR] User reject transfer failed: {ex.Message}");
                return StatusCode(500, new { error = "Internal server error" });
            }
        }

        // Provider accept transfer
        [HttpPost("/api/bookings/transfer/provider-accept")]
        [Authorize]
        [ServiceFilter(typeof(ServConnect.Filters.RequireApprovedUserApiFilter))]
        public async Task<IActionResult> ProviderAcceptTransfer([FromBody] TransferResponseRequest req)
        {
            try
            {
                var me = await _userManager.GetUserAsync(User);
                if (me == null) return Unauthorized();

                var success = await _transferService.ProviderAcceptTransferAsync(req.TransferId, me.Id, req.Message);
                if (!success)
                {
                    return BadRequest(new { error = "Failed to accept transfer request" });
                }

                return Ok(new { message = "Transfer request accepted successfully" });
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ERROR] Provider accept transfer failed: {ex.Message}");
                return StatusCode(500, new { error = "Internal server error" });
            }
        }

        // Provider reject transfer
        [HttpPost("/api/bookings/transfer/provider-reject")]
        [Authorize]
        [ServiceFilter(typeof(ServConnect.Filters.RequireApprovedUserApiFilter))]
        public async Task<IActionResult> ProviderRejectTransfer([FromBody] TransferResponseRequest req)
        {
            try
            {
                var me = await _userManager.GetUserAsync(User);
                if (me == null) return Unauthorized();

                var success = await _transferService.ProviderRejectTransferAsync(req.TransferId, me.Id, req.Message);
                if (!success)
                {
                    return BadRequest(new { error = "Failed to reject transfer request" });
                }

                return Ok(new { message = "Transfer request rejected successfully" });
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ERROR] Provider reject transfer failed: {ex.Message}");
                return StatusCode(500, new { error = "Internal server error" });
            }
        }

        // Cancel transfer request (Original provider)
        [HttpPost("/api/bookings/transfer/cancel")]
        [Authorize]
        [ServiceFilter(typeof(ServConnect.Filters.RequireApprovedUserApiFilter))]
        public async Task<IActionResult> CancelTransferRequest([FromBody] TransferResponseRequest req)
        {
            try
            {
                var me = await _userManager.GetUserAsync(User);
                if (me == null) return Unauthorized();

                var success = await _transferService.CancelTransferRequestAsync(req.TransferId, me.Id);
                if (!success)
                {
                    return BadRequest(new { error = "Failed to cancel transfer request" });
                }

                return Ok(new { message = "Transfer request cancelled successfully" });
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ERROR] Cancel transfer request failed: {ex.Message}");
                return StatusCode(500, new { error = "Internal server error" });
            }
        }

        // Get transfer requests for user
        [HttpGet("/api/bookings/transfer/user")]
        [Authorize]
        [ServiceFilter(typeof(ServConnect.Filters.RequireApprovedUserApiFilter))]
        public async Task<IActionResult> GetUserTransferRequests()
        {
            try
            {
                var me = await _userManager.GetUserAsync(User);
                if (me == null) return Unauthorized();

                var transfers = await _transferService.GetTransferRequestsForUserAsync(me.Id);
                
                return Ok(transfers.Select(t => new {
                    id = t.Id,
                    bookingId = t.BookingId,
                    originalProviderName = t.OriginalProviderName,
                    newProviderName = t.NewProviderName,
                    serviceName = t.ServiceName,
                    serviceDateTime = t.ServiceDateTime,
                    transferReason = t.TransferReason,
                    status = t.Status.ToString(),
                    requestedAt = t.RequestedAtUtc,
                    userMessage = t.UserMessage,
                    providerMessage = t.ProviderMessage
                }));
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ERROR] Get user transfer requests failed: {ex.Message}");
                return StatusCode(500, new { error = "Internal server error" });
            }
        }

        // Get transfer requests for provider
        [HttpGet("/api/bookings/transfer/provider")]
        [Authorize]
        [ServiceFilter(typeof(ServConnect.Filters.RequireApprovedUserApiFilter))]
        public async Task<IActionResult> GetProviderTransferRequests()
        {
            try
            {
                var me = await _userManager.GetUserAsync(User);
                if (me == null) return Unauthorized();

                var transfers = await _transferService.GetTransferRequestsForProviderAsync(me.Id);
                
                return Ok(transfers.Select(t => new {
                    id = t.Id,
                    bookingId = t.BookingId,
                    userName = t.UserName,
                    originalProviderName = t.OriginalProviderName,
                    newProviderName = t.NewProviderName,
                    serviceName = t.ServiceName,
                    serviceDateTime = t.ServiceDateTime,
                    transferReason = t.TransferReason,
                    status = t.Status.ToString(),
                    requestedAt = t.RequestedAtUtc,
                    userMessage = t.UserMessage,
                    providerMessage = t.ProviderMessage,
                    isOriginalProvider = t.OriginalProviderId == me.Id,
                    isNewProvider = t.NewProviderId == me.Id
                }));
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ERROR] Get provider transfer requests failed: {ex.Message}");
                return StatusCode(500, new { error = "Internal server error" });
            }
        }

        // Get pending transfer requests for provider (for notifications)
        [HttpGet("/api/bookings/transfer/provider/pending")]
        [Authorize]
        [ServiceFilter(typeof(ServConnect.Filters.RequireApprovedUserApiFilter))]
        public async Task<IActionResult> GetPendingTransferRequests()
        {
            try
            {
                var me = await _userManager.GetUserAsync(User);
                if (me == null) return Unauthorized();

                var transfers = await _transferService.GetPendingTransferRequestsForProviderAsync(me.Id);
                
                return Ok(transfers.Select(t => new {
                    id = t.Id,
                    bookingId = t.BookingId,
                    userName = t.UserName,
                    originalProviderName = t.OriginalProviderName,
                    serviceName = t.ServiceName,
                    serviceDateTime = t.ServiceDateTime,
                    transferReason = t.TransferReason,
                    requestedAt = t.RequestedAtUtc
                }));
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ERROR] Get pending transfer requests failed: {ex.Message}");
                return StatusCode(500, new { error = "Internal server error" });
            }
        }
    }
}