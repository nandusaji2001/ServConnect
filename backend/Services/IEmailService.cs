using System.Threading.Tasks;

namespace ServConnect.Services
{
    public interface IEmailService
    {
        Task SendEmailAsync(string toEmail, string subject, string body);
    }
}