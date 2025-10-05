using ServConnect.Models;

namespace ServConnect.Services
{
    public interface IServicePaymentService
    {
        Task<List<ServicePublicationPlan>> GetPublicationPlansAsync();
        Task<ServicePayment> CreatePaymentAsync(Guid providerId, string serviceName, string planName, int durationInMonths, decimal amountInRupees);
        Task<ServicePayment?> GetPaymentByIdAsync(string paymentId);
        Task<bool> MarkPaymentPaidAsync(string paymentId, string razorpayOrderId, string razorpayPaymentId, string razorpaySignature);
        Task<bool> ActivateServiceAsync(string paymentId, string providerServiceId);
        Task<List<ServicePayment>> GetPaymentsByProviderAsync(Guid providerId);
        Task DisableExpiredServicesAsync();
    }
}
