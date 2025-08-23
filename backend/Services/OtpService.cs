using System.Security.Cryptography;

namespace ServConnect.Services
{
    public class OtpService : IOtpService
    {
        private readonly ILogger<OtpService> _logger;
        private const int OtpLength = 6;
        private const int OtpExpiryMinutes = 10;

        public OtpService(ILogger<OtpService> logger)
        {
            _logger = logger;
        }

        public string GenerateOtp()
        {
            using var rng = RandomNumberGenerator.Create();
            var bytes = new byte[4];
            rng.GetBytes(bytes);
            
            var randomNumber = Math.Abs(BitConverter.ToInt32(bytes, 0));
            var otp = (randomNumber % 1000000).ToString("D6");
            
            _logger.LogInformation("OTP generated successfully");
            return otp;
        }

        public bool ValidateOtp(string providedOtp, string storedOtp, DateTime? expiryTime)
        {
            if (string.IsNullOrEmpty(providedOtp) || string.IsNullOrEmpty(storedOtp))
            {
                _logger.LogWarning("OTP validation failed: Empty OTP provided or stored");
                return false;
            }

            if (IsOtpExpired(expiryTime))
            {
                _logger.LogWarning("OTP validation failed: OTP has expired");
                return false;
            }

            var isValid = providedOtp.Equals(storedOtp, StringComparison.Ordinal);
            _logger.LogInformation("OTP validation result: {IsValid}", isValid);
            
            return isValid;
        }

        public bool IsOtpExpired(DateTime? expiryTime)
        {
            if (!expiryTime.HasValue)
            {
                return true;
            }

            return DateTime.UtcNow > expiryTime.Value;
        }

        public DateTime GetOtpExpiryTime()
        {
            return DateTime.UtcNow.AddMinutes(OtpExpiryMinutes);
        }
    }
}