using ServConnect.Models;

namespace ServConnect.Services
{
    public interface IBookingPaymentService
    {
        Task<BookingPayment> CreatePaymentAsync(Guid userId, string userName, string userEmail, 
            string bookingId, string serviceName, string providerName, decimal amountInRupees, 
            int? userRating, string? userFeedback);
        Task<BookingPayment?> GetPaymentByIdAsync(string paymentId);
        Task<BookingPayment?> GetPaymentByBookingIdAsync(string bookingId);
        Task<bool> MarkPaymentPaidAsync(string paymentId, string razorpayOrderId, string razorpayPaymentId, string razorpaySignature);
        Task<List<BookingPayment>> GetPaymentsByUserAsync(Guid userId);
        Task<bool> HasPendingPaymentsAsync(Guid userId);
        Task<List<BookingPayment>> GetPendingPaymentsByUserAsync(Guid userId);
    }
}
