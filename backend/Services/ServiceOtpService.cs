using Microsoft.Extensions.Configuration;
using MongoDB.Driver;
using ServConnect.Models;

namespace ServConnect.Services
{
    public class ServiceOtpService : IServiceOtpService
    {
        private readonly IMongoCollection<ServiceOtp> _otps;
        private readonly INotificationService _notificationService;

        public ServiceOtpService(IConfiguration config, INotificationService notificationService)
        {
            var conn = config["MongoDB:ConnectionString"] ?? "mongodb://localhost:27017";
            var dbName = config["MongoDB:DatabaseName"] ?? "ServConnectDb";
            var client = new MongoClient(conn);
            var db = client.GetDatabase(dbName);
            _otps = db.GetCollection<ServiceOtp>("ServiceOtps");
            _notificationService = notificationService;
        }

        public async Task<ServiceOtp> GenerateOtpAsync(string bookingId, Guid userId, Guid providerId, string serviceName, string providerName)
        {
            // Invalidate any existing OTP for this booking
            await InvalidateExistingOtpAsync(bookingId);

            // Generate 6-digit random code
            var random = new Random();
            var otpCode = random.Next(100000, 999999).ToString();

            var otp = new ServiceOtp
            {
                BookingId = bookingId,
                UserId = userId,
                ProviderId = providerId,
                OtpCode = otpCode,
                GeneratedAt = DateTime.UtcNow,
                ExpiresAt = DateTime.UtcNow.AddMinutes(10), // 10 minutes validity
                ServiceName = serviceName,
                ProviderName = providerName
            };

            await _otps.InsertOneAsync(otp);

            // Send notification to user
            await _notificationService.CreateNotificationAsync(
                userId.ToString(),
                "Service Start Code",
                $"Your service start code for {serviceName} by {providerName} is: {otpCode}. Valid for 10 minutes.",
                NotificationType.ServiceOtp
            );

            return otp;
        }

        public async Task<bool> ValidateOtpAsync(string bookingId, string otpCode, Guid providerId)
        {
            var filter = Builders<ServiceOtp>.Filter.And(
                Builders<ServiceOtp>.Filter.Eq(x => x.BookingId, bookingId),
                Builders<ServiceOtp>.Filter.Eq(x => x.OtpCode, otpCode),
                Builders<ServiceOtp>.Filter.Eq(x => x.ProviderId, providerId),
                Builders<ServiceOtp>.Filter.Eq(x => x.IsUsed, false),
                Builders<ServiceOtp>.Filter.Gt(x => x.ExpiresAt, DateTime.UtcNow)
            );

            var otp = await _otps.Find(filter).FirstOrDefaultAsync();
            
            if (otp != null)
            {
                // Mark as used
                await MarkOtpAsUsedAsync(otp.Id!);
                return true;
            }

            return false;
        }

        public async Task<ServiceOtp?> GetActiveOtpAsync(string bookingId)
        {
            var filter = Builders<ServiceOtp>.Filter.And(
                Builders<ServiceOtp>.Filter.Eq(x => x.BookingId, bookingId),
                Builders<ServiceOtp>.Filter.Eq(x => x.IsUsed, false),
                Builders<ServiceOtp>.Filter.Gt(x => x.ExpiresAt, DateTime.UtcNow)
            );

            return await _otps.Find(filter).FirstOrDefaultAsync();
        }

        public async Task<List<ServiceOtp>> GetActiveOtpsForUserAsync(Guid userId)
        {
            var filter = Builders<ServiceOtp>.Filter.And(
                Builders<ServiceOtp>.Filter.Eq(x => x.UserId, userId),
                Builders<ServiceOtp>.Filter.Eq(x => x.IsUsed, false),
                Builders<ServiceOtp>.Filter.Gt(x => x.ExpiresAt, DateTime.UtcNow)
            );

            return await _otps.Find(filter).ToListAsync();
        }

        public async Task<bool> MarkOtpAsUsedAsync(string otpId)
        {
            var update = Builders<ServiceOtp>.Update
                .Set(x => x.IsUsed, true)
                .Set(x => x.UsedAt, DateTime.UtcNow);

            var result = await _otps.UpdateOneAsync(x => x.Id == otpId, update);
            return result.ModifiedCount == 1;
        }

        public async Task<int> CleanupExpiredOtpsAsync()
        {
            var filter = Builders<ServiceOtp>.Filter.Lt(x => x.ExpiresAt, DateTime.UtcNow);
            var result = await _otps.DeleteManyAsync(filter);
            return (int)result.DeletedCount;
        }

        private async Task InvalidateExistingOtpAsync(string bookingId)
        {
            var update = Builders<ServiceOtp>.Update.Set(x => x.IsUsed, true);
            var filter = Builders<ServiceOtp>.Filter.And(
                Builders<ServiceOtp>.Filter.Eq(x => x.BookingId, bookingId),
                Builders<ServiceOtp>.Filter.Eq(x => x.IsUsed, false)
            );

            await _otps.UpdateManyAsync(filter, update);
        }
    }
}
