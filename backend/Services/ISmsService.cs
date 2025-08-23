namespace ServConnect.Services
{
    public interface ISmsService
    {
        Task<bool> SendSmsAsync(string phoneNumber, string message);
        Task<bool> SendOtpAsync(string phoneNumber, string otp);
    }
}