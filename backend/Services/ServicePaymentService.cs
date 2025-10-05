using MongoDB.Driver;
using ServConnect.Models;
using Microsoft.Extensions.Configuration;

namespace ServConnect.Services
{
    public class ServicePaymentService : IServicePaymentService
    {
        private readonly IMongoCollection<ServicePayment> _payments;
        private readonly IMongoCollection<ProviderService> _providerServices;

        public ServicePaymentService(IConfiguration config)
        {
            var conn = config["MongoDB:ConnectionString"] ?? "mongodb://localhost:27017";
            var dbName = config["MongoDB:DatabaseName"] ?? "ServConnectDb";
            var client = new MongoClient(conn);
            var db = client.GetDatabase(dbName);
            _payments = db.GetCollection<ServicePayment>("ServicePayments");
            _providerServices = db.GetCollection<ProviderService>("ProviderServices");
        }

        public async Task<List<ServicePublicationPlan>> GetPublicationPlansAsync()
        {
            return ServicePublicationPlan.GetPredefinedPlans();
        }

        public async Task<ServicePayment> CreatePaymentAsync(Guid providerId, string serviceName, string planName, int durationInMonths, decimal amountInRupees)
        {
            var payment = new ServicePayment
            {
                ProviderId = providerId,
                ServiceName = serviceName,
                PlanName = planName,
                DurationInMonths = durationInMonths,
                AmountInRupees = amountInRupees,
                Status = ServicePaymentStatus.Pending
            };

            await _payments.InsertOneAsync(payment);
            return payment;
        }

        public async Task<ServicePayment?> GetPaymentByIdAsync(string paymentId)
        {
            return await _payments.Find(p => p.Id == paymentId).FirstOrDefaultAsync();
        }

        public async Task<bool> MarkPaymentPaidAsync(string paymentId, string razorpayOrderId, string razorpayPaymentId, string razorpaySignature)
        {
            var filter = Builders<ServicePayment>.Filter.Eq(p => p.Id, paymentId);
            var update = Builders<ServicePayment>.Update
                .Set(p => p.Status, ServicePaymentStatus.Paid)
                .Set(p => p.RazorpayOrderId, razorpayOrderId)
                .Set(p => p.RazorpayPaymentId, razorpayPaymentId)
                .Set(p => p.RazorpaySignature, razorpaySignature)
                .Set(p => p.UpdatedAt, DateTime.UtcNow);

            var result = await _payments.UpdateOneAsync(filter, update);
            return result.ModifiedCount > 0;
        }

        public async Task<bool> ActivateServiceAsync(string paymentId, string providerServiceId)
        {
            var payment = await GetPaymentByIdAsync(paymentId);
            if (payment == null || payment.Status != ServicePaymentStatus.Paid)
                return false;

            var startDate = DateTime.UtcNow;
            var endDate = startDate.AddMonths(payment.DurationInMonths);

            // Update payment with service link and dates
            var paymentFilter = Builders<ServicePayment>.Filter.Eq(p => p.Id, paymentId);
            var paymentUpdate = Builders<ServicePayment>.Update
                .Set(p => p.ProviderServiceId, providerServiceId)
                .Set(p => p.PublicationStartDate, startDate)
                .Set(p => p.PublicationEndDate, endDate)
                .Set(p => p.UpdatedAt, DateTime.UtcNow);

            await _payments.UpdateOneAsync(paymentFilter, paymentUpdate);

            // Update provider service with payment info and expiry
            var serviceFilter = Builders<ProviderService>.Filter.Eq(s => s.Id, providerServiceId);
            var serviceUpdate = Builders<ProviderService>.Update
                .Set(s => s.IsPaid, true)
                .Set(s => s.PaymentId, paymentId)
                .Set(s => s.PublicationStartDate, startDate)
                .Set(s => s.PublicationEndDate, endDate)
                .Set(s => s.IsActive, true)
                .Set(s => s.UpdatedAt, DateTime.UtcNow);

            var result = await _providerServices.UpdateOneAsync(serviceFilter, serviceUpdate);
            return result.ModifiedCount > 0;
        }

        public async Task<List<ServicePayment>> GetPaymentsByProviderAsync(Guid providerId)
        {
            return await _payments.Find(p => p.ProviderId == providerId)
                .SortByDescending(p => p.CreatedAt)
                .ToListAsync();
        }

        public async Task DisableExpiredServicesAsync()
        {
            var now = DateTime.UtcNow;
            var filter = Builders<ProviderService>.Filter.And(
                Builders<ProviderService>.Filter.Eq(s => s.IsActive, true),
                Builders<ProviderService>.Filter.Lt(s => s.PublicationEndDate, now),
                Builders<ProviderService>.Filter.Ne(s => s.PublicationEndDate, null)
            );

            var update = Builders<ProviderService>.Update
                .Set(s => s.IsActive, false)
                .Set(s => s.UpdatedAt, DateTime.UtcNow);

            await _providerServices.UpdateManyAsync(filter, update);
        }
    }
}
