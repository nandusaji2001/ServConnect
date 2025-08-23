namespace ServConnect.Services
{
    public interface IOtpService
    {
        string GenerateOtp();
        bool ValidateOtp(string providedOtp, string storedOtp, DateTime? expiryTime);
        bool IsOtpExpired(DateTime? expiryTime);
        DateTime GetOtpExpiryTime();
    }
}