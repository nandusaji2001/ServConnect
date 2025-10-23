using ServConnect.Models;

namespace ServConnect.Services
{
    public interface IServiceOtpService
    {
        /// <summary>
        /// Generates a new 6-digit OTP for service start verification
        /// </summary>
        /// <param name="bookingId">The booking ID</param>
        /// <param name="userId">User who will receive the OTP</param>
        /// <param name="providerId">Provider who will enter the OTP</param>
        /// <param name="serviceName">Name of the service</param>
        /// <param name="providerName">Name of the provider</param>
        /// <returns>Generated ServiceOtp</returns>
        Task<ServiceOtp> GenerateOtpAsync(string bookingId, Guid userId, Guid providerId, string serviceName, string providerName);

        /// <summary>
        /// Validates an OTP code for a specific booking
        /// </summary>
        /// <param name="bookingId">The booking ID</param>
        /// <param name="otpCode">The 6-digit OTP code</param>
        /// <param name="providerId">Provider attempting to validate</param>
        /// <returns>True if OTP is valid and not expired</returns>
        Task<bool> ValidateOtpAsync(string bookingId, string otpCode, Guid providerId);

        /// <summary>
        /// Gets the active OTP for a booking
        /// </summary>
        /// <param name="bookingId">The booking ID</param>
        /// <returns>Active ServiceOtp or null if none exists</returns>
        Task<ServiceOtp?> GetActiveOtpAsync(string bookingId);

        /// <summary>
        /// Gets all active OTPs for a user (for notification display)
        /// </summary>
        /// <param name="userId">The user ID</param>
        /// <returns>List of active OTPs</returns>
        Task<List<ServiceOtp>> GetActiveOtpsForUserAsync(Guid userId);

        /// <summary>
        /// Marks an OTP as used
        /// </summary>
        /// <param name="otpId">The OTP ID</param>
        /// <returns>True if successfully marked as used</returns>
        Task<bool> MarkOtpAsUsedAsync(string otpId);

        /// <summary>
        /// Cleans up expired OTPs
        /// </summary>
        /// <returns>Number of expired OTPs removed</returns>
        Task<int> CleanupExpiredOtpsAsync();
    }
}
