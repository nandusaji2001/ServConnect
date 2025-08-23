using AspNetCore.Identity.MongoDbCore.Models;
using MongoDbGenericRepository.Attributes;

namespace ServConnect.Models
{
    [CollectionName("Users")]
    public class Users : MongoIdentityUser<Guid>
    {
        public string FullName { get; set; } = string.Empty;
        
        // Firebase Integration
        public string? FirebaseUid { get; set; }
        
        // Contact Information
        public string? Address { get; set; } = string.Empty;
        
        // OTP and Password Reset
        public string? PasswordResetOtp { get; set; }
        public DateTime? OtpExpiryTime { get; set; }
        public int OtpAttempts { get; set; } = 0;
        public DateTime? LastOtpRequestTime { get; set; }

        // Role-specific properties
        public string? LicenseNumber { get; set; } // For ServiceProviders
        public string? Organization { get; set; }  // For Admins/Providers
        public string? BusinessName { get; set; }  // For Vendors
        public string? BusinessRegistrationNumber { get; set; } // For Vendors
        public string? BusinessAddress { get; set; } // For Vendors
    }
}