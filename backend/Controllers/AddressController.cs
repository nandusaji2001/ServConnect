using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using ServConnect.Models;
using ServConnect.Services;

namespace ServConnect.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class AddressController : ControllerBase
    {
        private readonly IAddressService _addressService;
        private readonly UserManager<Users> _userManager;

        public AddressController(IAddressService addressService, UserManager<Users> userManager)
        {
            _addressService = addressService;
            _userManager = userManager;
        }

        [HttpGet]
        public async Task<IActionResult> GetMyAddresses()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return Unauthorized();

            var addresses = await _addressService.GetUserAddressesAsync(user.Id);
            return Ok(addresses);
        }

        [HttpGet("{id}")]
        public async Task<IActionResult> GetAddress(string id)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return Unauthorized();

            var address = await _addressService.GetAddressByIdAsync(id, user.Id);
            if (address == null) return NotFound();

            return Ok(address);
        }

        [HttpPost]
        public async Task<IActionResult> CreateAddress([FromBody] CreateAddressRequest request)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return Unauthorized();

            if (!ModelState.IsValid) return BadRequest(ModelState);

            var address = new UserAddress
            {
                UserId = user.Id,
                Label = request.Label,
                FullName = request.FullName,
                PhoneNumber = request.PhoneNumber,
                AddressLine1 = request.AddressLine1,
                AddressLine2 = request.AddressLine2,
                City = request.City,
                State = request.State,
                PostalCode = request.PostalCode,
                Country = request.Country ?? "India",
                Landmark = request.Landmark,
                IsDefault = request.IsDefault
            };

            var created = await _addressService.CreateAddressAsync(address);
            return Ok(created);
        }

        [HttpPut("{id}")]
        public async Task<IActionResult> UpdateAddress(string id, [FromBody] UpdateAddressRequest request)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return Unauthorized();

            if (!ModelState.IsValid) return BadRequest(ModelState);

            var address = new UserAddress
            {
                Label = request.Label,
                FullName = request.FullName,
                PhoneNumber = request.PhoneNumber,
                AddressLine1 = request.AddressLine1,
                AddressLine2 = request.AddressLine2,
                City = request.City,
                State = request.State,
                PostalCode = request.PostalCode,
                Country = request.Country ?? "India",
                Landmark = request.Landmark,
                IsDefault = request.IsDefault
            };

            var updated = await _addressService.UpdateAddressAsync(id, address, user.Id);
            if (!updated) return NotFound();

            return Ok();
        }

        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteAddress(string id)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return Unauthorized();

            var deleted = await _addressService.DeleteAddressAsync(id, user.Id);
            if (!deleted) return NotFound();

            return Ok();
        }

        [HttpPost("{id}/set-default")]
        public async Task<IActionResult> SetDefaultAddress(string id)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return Unauthorized();

            var updated = await _addressService.SetDefaultAddressAsync(id, user.Id);
            if (!updated) return NotFound();

            return Ok();
        }

        [HttpGet("default")]
        public async Task<IActionResult> GetDefaultAddress()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return Unauthorized();

            var address = await _addressService.GetDefaultAddressAsync(user.Id);
            if (address == null) return NotFound();

            return Ok(address);
        }
    }

    public class CreateAddressRequest
    {
        public string Label { get; set; } = string.Empty;
        public string FullName { get; set; } = string.Empty;
        public string PhoneNumber { get; set; } = string.Empty;
        public string AddressLine1 { get; set; } = string.Empty;
        public string? AddressLine2 { get; set; }
        public string City { get; set; } = string.Empty;
        public string State { get; set; } = string.Empty;
        public string PostalCode { get; set; } = string.Empty;
        public string? Country { get; set; }
        public string? Landmark { get; set; }
        public bool IsDefault { get; set; } = false;
    }

    public class UpdateAddressRequest
    {
        public string Label { get; set; } = string.Empty;
        public string FullName { get; set; } = string.Empty;
        public string PhoneNumber { get; set; } = string.Empty;
        public string AddressLine1 { get; set; } = string.Empty;
        public string? AddressLine2 { get; set; }
        public string City { get; set; } = string.Empty;
        public string State { get; set; } = string.Empty;
        public string PostalCode { get; set; } = string.Empty;
        public string? Country { get; set; }
        public string? Landmark { get; set; }
        public bool IsDefault { get; set; } = false;
    }
}
