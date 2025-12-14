using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using ServConnect.Models;
using ServConnect.Services;

namespace ServConnect.Controllers
{
    public class RentalController : Controller
    {
        private readonly IRentalPropertyService _rentalService;
        private readonly UserManager<Users> _userManager;
        private readonly ILogger<RentalController> _logger;

        public RentalController(
            IRentalPropertyService rentalService,
            UserManager<Users> userManager,
            ILogger<RentalController> logger)
        {
            _rentalService = rentalService;
            _userManager = userManager;
            _logger = logger;
        }

        // Main House Rentals listing page
        [AllowAnonymous]
        public IActionResult Index()
        {
            return View("HouseRentals");
        }

        // Property Details page
        [AllowAnonymous]
        public async Task<IActionResult> Details(string id)
        {
            if (string.IsNullOrEmpty(id))
            {
                return NotFound();
            }

            var property = await _rentalService.GetByIdAsync(id);
            if (property == null)
            {
                return NotFound();
            }

            // Increment view count
            await _rentalService.IncrementViewCountAsync(id);

            return View("PropertyDetails", property);
        }

        // Add Property page
        [Authorize]
        public IActionResult AddProperty()
        {
            return View();
        }

        // Manage Listings page
        [Authorize]
        public IActionResult ManageListings()
        {
            return View();
        }

        // Edit Property page
        [Authorize]
        public async Task<IActionResult> EditProperty(string id)
        {
            if (string.IsNullOrEmpty(id))
            {
                return NotFound();
            }

            var property = await _rentalService.GetByIdAsync(id);
            if (property == null)
            {
                return NotFound();
            }

            var userId = _userManager.GetUserId(User);
            if (property.OwnerId != userId && !User.IsInRole(RoleTypes.Admin))
            {
                return Forbid();
            }

            return View(property);
        }

        // My Queries page - for users to view their rental queries
        [Authorize]
        public IActionResult MyQueries()
        {
            return View();
        }
    }

    // API Controller for rental properties
    [ApiController]
    [Route("api/[controller]")]
    public class RentalApiController : ControllerBase
    {
        private readonly IRentalPropertyService _rentalService;
        private readonly IRentalSubscriptionService _subscriptionService;
        private readonly UserManager<Users> _userManager;
        private readonly ILogger<RentalApiController> _logger;

        public RentalApiController(
            IRentalPropertyService rentalService,
            IRentalSubscriptionService subscriptionService,
            UserManager<Users> userManager,
            ILogger<RentalApiController> logger)
        {
            _rentalService = rentalService;
            _subscriptionService = subscriptionService;
            _userManager = userManager;
            _logger = logger;
        }

        // GET: api/rentalapi/properties
        [HttpGet("properties")]
        public async Task<IActionResult> GetProperties(
            [FromQuery] string? q,
            [FromQuery] HouseType? houseType,
            [FromQuery] FurnishingType? furnishing,
            [FromQuery] string? city,
            [FromQuery] decimal? minRent,
            [FromQuery] decimal? maxRent,
            [FromQuery] string? amenities,
            [FromQuery] int skip = 0,
            [FromQuery] int take = 20)
        {
            try
            {
                List<string>? amenitiesList = null;
                if (!string.IsNullOrEmpty(amenities))
                {
                    amenitiesList = amenities.Split(',').Select(a => a.Trim()).ToList();
                }

                // Get current user ID to exclude their own properties
                string? excludeOwnerId = null;
                if (User.Identity?.IsAuthenticated == true)
                {
                    excludeOwnerId = _userManager.GetUserId(User);
                }

                var properties = await _rentalService.SearchAsync(q, houseType, furnishing, city, minRent, maxRent, amenitiesList, skip, take, excludeOwnerId);
                var total = await _rentalService.GetTotalCountAsync(excludeOwnerId);

                return Ok(new { properties, total });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching rental properties");
                return StatusCode(500, new { error = "Failed to fetch properties" });
            }
        }

        // GET: api/rentalapi/properties/{id}
        [HttpGet("properties/{id}")]
        public async Task<IActionResult> GetProperty(string id)
        {
            try
            {
                var property = await _rentalService.GetByIdAsync(id);
                if (property == null)
                {
                    return NotFound(new { success = false, error = "Property not found" });
                }

                // Check if current user is the owner
                bool isOwner = false;
                bool hasSubscription = false;
                string? userId = null;

                if (User.Identity?.IsAuthenticated == true)
                {
                    userId = _userManager.GetUserId(User);
                    isOwner = property.OwnerId == userId;
                    
                    // Check subscription status if not owner
                    if (!isOwner && userId != null)
                    {
                        hasSubscription = await _subscriptionService.HasActiveSubscriptionAsync(userId);
                    }
                }

                // If owner or has subscription, show full contact details
                // Otherwise, mask/hide contact information
                object responseProperty;
                if (isOwner || hasSubscription)
                {
                    responseProperty = property;
                }
                else
                {
                    // Create a masked version of the property
                    responseProperty = new
                    {
                        property.Id,
                        property.PropertyId,
                        property.Title,
                        property.Description,
                        property.HouseType,
                        property.FullAddress,
                        property.City,
                        property.Area,
                        property.Pincode,
                        property.RentAmount,
                        property.DepositAmount,
                        property.Bedrooms,
                        property.Bathrooms,
                        property.SquareFeet,
                        property.FloorNumber,
                        property.TotalFloors,
                        property.Furnishing,
                        property.Amenities,
                        property.ImageUrls,
                        property.IsAvailable,
                        property.IsPaused,
                        property.Latitude,
                        property.Longitude,
                        property.CreatedAt,
                        property.OwnerId,
                        property.BachelorsAllowed,
                        property.PetsAllowed,
                        // Mask contact details
                        OwnerName = MaskString(property.OwnerName),
                        OwnerPhone = "**********",
                        OwnerEmail = MaskEmail(property.OwnerEmail)
                    };
                }

                return Ok(new { success = true, property = responseProperty, isOwner, hasSubscription });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching property {Id}", id);
                return StatusCode(500, new { success = false, error = "Failed to fetch property" });
            }
        }

        // Helper method to mask strings
        private string MaskString(string? value)
        {
            if (string.IsNullOrEmpty(value) || value.Length <= 2)
                return "***";
            return value[0] + new string('*', Math.Min(value.Length - 2, 5)) + value[^1];
        }

        // Helper method to mask email
        private string MaskEmail(string? email)
        {
            if (string.IsNullOrEmpty(email))
                return "***@***.***";
            var parts = email.Split('@');
            if (parts.Length != 2)
                return "***@***.***";
            return MaskString(parts[0]) + "@***." + (parts[1].Contains('.') ? parts[1].Split('.').Last() : "***");
        }

        // GET: api/rentalapi/my-properties
        [Authorize]
        [HttpGet("my-properties")]
        public async Task<IActionResult> GetMyProperties()
        {
            try
            {
                var userId = _userManager.GetUserId(User);
                if (string.IsNullOrEmpty(userId))
                {
                    return Unauthorized(new { success = false, error = "Unauthorized" });
                }

                var properties = await _rentalService.GetByOwnerAsync(userId);
                return Ok(new { success = true, properties });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching user properties");
                return StatusCode(500, new { success = false, error = "Failed to fetch your properties" });
            }
        }

        // POST: api/rentalapi/properties
        [Authorize]
        [HttpPost("properties")]
        public async Task<IActionResult> CreateProperty([FromBody] RentalPropertyDto dto)
        {
            try
            {
                var user = await _userManager.GetUserAsync(User);
                if (user == null)
                {
                    return Unauthorized();
                }

                var property = new RentalProperty
                {
                    Title = dto.Title,
                    Description = dto.Description,
                    HouseType = dto.HouseType,
                    RentAmount = dto.RentAmount,
                    DepositAmount = dto.DepositAmount,
                    FullAddress = dto.FullAddress,
                    City = dto.City,
                    Area = dto.Area,
                    Pincode = dto.Pincode,
                    Latitude = dto.Latitude,
                    Longitude = dto.Longitude,
                    Furnishing = dto.Furnishing,
                    Amenities = dto.Amenities,
                    ImageUrls = dto.ImageUrls,
                    IsAvailable = dto.IsAvailable,
                    OwnerId = user.Id.ToString(),
                    OwnerName = dto.OwnerName.Length > 0 ? dto.OwnerName : user.FullName ?? user.UserName ?? "",
                    OwnerPhone = dto.OwnerPhone.Length > 0 ? dto.OwnerPhone : user.PhoneNumber ?? "",
                    OwnerEmail = dto.OwnerEmail.Length > 0 ? dto.OwnerEmail : user.Email ?? "",
                    Bedrooms = dto.Bedrooms,
                    Bathrooms = dto.Bathrooms,
                    SquareFeet = dto.SquareFeet,
                    FloorNumber = dto.FloorNumber,
                    TotalFloors = dto.TotalFloors,
                    PetsAllowed = dto.PetsAllowed,
                    BachelorsAllowed = dto.BachelorsAllowed
                };

                var created = await _rentalService.CreateAsync(property);
                return Ok(new { success = true, property = created, message = $"Property listed successfully! ID: {created.PropertyId}" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating property");
                return StatusCode(500, new { error = "Failed to create property" });
            }
        }

        // PUT: api/rentalapi/properties/{id}
        [Authorize]
        [HttpPut("properties/{id}")]
        public async Task<IActionResult> UpdateProperty(string id, [FromBody] RentalPropertyDto dto)
        {
            try
            {
                var userId = _userManager.GetUserId(User);
                var existing = await _rentalService.GetByIdAsync(id);

                if (existing == null)
                {
                    return NotFound(new { error = "Property not found" });
                }

                if (existing.OwnerId != userId && !User.IsInRole(RoleTypes.Admin))
                {
                    return Forbid();
                }

                existing.Title = dto.Title;
                existing.Description = dto.Description;
                existing.HouseType = dto.HouseType;
                existing.RentAmount = dto.RentAmount;
                existing.DepositAmount = dto.DepositAmount;
                existing.FullAddress = dto.FullAddress;
                existing.City = dto.City;
                existing.Area = dto.Area;
                existing.Pincode = dto.Pincode;
                existing.Latitude = dto.Latitude;
                existing.Longitude = dto.Longitude;
                existing.Furnishing = dto.Furnishing;
                existing.Amenities = dto.Amenities;
                existing.ImageUrls = dto.ImageUrls;
                existing.IsAvailable = dto.IsAvailable;
                existing.OwnerName = dto.OwnerName;
                existing.OwnerPhone = dto.OwnerPhone;
                existing.OwnerEmail = dto.OwnerEmail;
                existing.Bedrooms = dto.Bedrooms;
                existing.Bathrooms = dto.Bathrooms;
                existing.SquareFeet = dto.SquareFeet;
                existing.FloorNumber = dto.FloorNumber;
                existing.TotalFloors = dto.TotalFloors;
                existing.PetsAllowed = dto.PetsAllowed;
                existing.BachelorsAllowed = dto.BachelorsAllowed;

                var updated = await _rentalService.UpdateAsync(id, existing);
                return Ok(new { success = updated, message = updated ? "Property updated successfully!" : "Failed to update property" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating property {Id}", id);
                return StatusCode(500, new { error = "Failed to update property" });
            }
        }

        // DELETE: api/rentalapi/properties/{id}
        [Authorize]
        [HttpDelete("properties/{id}")]
        public async Task<IActionResult> DeleteProperty(string id)
        {
            try
            {
                var userId = _userManager.GetUserId(User);
                var existing = await _rentalService.GetByIdAsync(id);

                if (existing == null)
                {
                    return NotFound(new { error = "Property not found" });
                }

                if (existing.OwnerId != userId && !User.IsInRole(RoleTypes.Admin))
                {
                    return Forbid();
                }

                var deleted = await _rentalService.DeleteAsync(id);
                return Ok(new { success = deleted, message = deleted ? "Property deleted successfully!" : "Failed to delete property" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting property {Id}", id);
                return StatusCode(500, new { error = "Failed to delete property" });
            }
        }

        // POST: api/rentalapi/properties/{id}/toggle-pause
        [Authorize]
        [HttpPost("properties/{id}/toggle-pause")]
        public async Task<IActionResult> TogglePause(string id)
        {
            try
            {
                var userId = _userManager.GetUserId(User);
                var existing = await _rentalService.GetByIdAsync(id);

                if (existing == null)
                {
                    return NotFound(new { error = "Property not found" });
                }

                if (existing.OwnerId != userId && !User.IsInRole(RoleTypes.Admin))
                {
                    return Forbid();
                }

                var toggled = await _rentalService.TogglePauseAsync(id);
                var newStatus = !existing.IsPaused;
                return Ok(new { 
                    success = toggled, 
                    isPaused = newStatus,
                    message = newStatus ? "Property listing paused" : "Property listing resumed" 
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error toggling pause for property {Id}", id);
                return StatusCode(500, new { error = "Failed to toggle property status" });
            }
        }

        // POST: api/rentalapi/inquiries
        [Authorize]
        [HttpPost("inquiries")]
        public async Task<IActionResult> CreateInquiry([FromBody] RentalInquiryDto dto)
        {
            try
            {
                var user = await _userManager.GetUserAsync(User);
                if (user == null)
                {
                    return Unauthorized();
                }

                var property = await _rentalService.GetByIdAsync(dto.PropertyId);
                if (property == null)
                {
                    return NotFound(new { error = "Property not found" });
                }

                var inquiry = new RentalInquiry
                {
                    PropertyId = dto.PropertyId,
                    UserId = user.Id.ToString(),
                    UserName = user.FullName ?? user.UserName ?? "",
                    UserPhone = dto.UserPhone,
                    UserEmail = user.Email ?? "",
                    Message = dto.Message
                };

                var created = await _rentalService.CreateInquiryAsync(inquiry);
                return Ok(new { success = true, message = "Inquiry sent successfully! The owner will contact you soon." });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating inquiry");
                return StatusCode(500, new { error = "Failed to send inquiry" });
            }
        }

        // GET: api/rentalapi/my-inquiries
        [Authorize]
        [HttpGet("my-inquiries")]
        public async Task<IActionResult> GetMyInquiries()
        {
            try
            {
                var userId = _userManager.GetUserId(User);
                if (string.IsNullOrEmpty(userId))
                {
                    return Unauthorized(new { success = false, error = "Unauthorized" });
                }

                var inquiries = await _rentalService.GetInquiriesByOwnerAsync(userId);
                return Ok(new { success = true, inquiries });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching inquiries");
                return StatusCode(500, new { success = false, error = "Failed to fetch inquiries" });
            }
        }
    }

    public class RentalInquiryDto
    {
        public string PropertyId { get; set; } = string.Empty;
        public string UserPhone { get; set; } = string.Empty;
        public string Message { get; set; } = string.Empty;
    }
}
