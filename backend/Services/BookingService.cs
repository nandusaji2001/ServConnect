using Microsoft.Extensions.Configuration;
using MongoDB.Driver;
using ServConnect.Models;

namespace ServConnect.Services
{
    public class BookingService : IBookingService
    {
        private readonly IMongoCollection<Booking> _bookings;
        private readonly IMongoCollection<Users> _users;
        private readonly IMongoCollection<ProviderService> _providerServices;

        public BookingService(IConfiguration config)
        {
            var conn = config["MongoDB:ConnectionString"] ?? "mongodb://localhost:27017";
            var dbName = config["MongoDB:DatabaseName"] ?? "ServConnectDb";
            var client = new MongoClient(conn);
            var db = client.GetDatabase(dbName);
            _bookings = db.GetCollection<Booking>("Bookings");
            _users = db.GetCollection<Users>("Users");
            _providerServices = db.GetCollection<ProviderService>("ProviderServices");
        }

        public async Task<Booking> CreateAsync(Guid userId, string userName, string userEmail, Guid providerId, string providerName, string providerServiceId, string serviceName, DateTime serviceDateTime, string contactPhone, string address, string? note)
        {
            // Fetch price information from ProviderService
            var providerService = await _providerServices.Find(x => x.Id == providerServiceId).FirstOrDefaultAsync();
            var price = providerService?.Price ?? 0;
            var priceUnit = providerService?.PriceUnit ?? "per service";
            var currency = providerService?.Currency ?? "USD";

            var booking = new Booking
            {
                Id = null!,
                UserId = userId,
                UserName = userName,
                UserEmail = userEmail,
                ProviderId = providerId,
                ProviderName = providerName,
                ProviderServiceId = providerServiceId,
                ServiceName = serviceName,
                ServiceDateTime = serviceDateTime,
                ContactPhone = contactPhone,
                Address = address,
                Note = note,
                Price = price,
                PriceUnit = priceUnit,
                Currency = currency,
                RequestedAtUtc = DateTime.UtcNow,
                Status = BookingStatus.Pending
            };
            await _bookings.InsertOneAsync(booking);
            return booking;
        }

        public async Task<List<Booking>> GetForProviderAsync(Guid providerId)
        {
            return await _bookings.Find(x => x.ProviderId == providerId)
                                  .SortByDescending(x => x.RequestedAtUtc)
                                  .ToListAsync();
        }

        public async Task<List<Booking>> GetForUserAsync(Guid userId)
        {
            return await _bookings.Find(x => x.UserId == userId)
                                  .SortByDescending(x => x.RequestedAtUtc)
                                  .ToListAsync();
        }

        public async Task<Booking?> GetByIdAsync(string id)
        {
            return await _bookings.Find(x => x.Id == id).FirstOrDefaultAsync();
        }

        public async Task<bool> SetStatusAsync(string id, Guid providerId, BookingStatus status, string? providerMessage)
        {
            var update = Builders<Booking>.Update
                .Set(x => x.Status, status)
                .Set(x => x.ProviderMessage, providerMessage)
                .Set(x => x.RespondedAtUtc, DateTime.UtcNow);
            var res = await _bookings.UpdateOneAsync(x => x.Id == id && x.ProviderId == providerId, update);
            return res.ModifiedCount == 1;
        }

        public async Task<bool> CompleteAsync(string id, Guid userId, int? rating, string? feedback)
        {
            // Validate rating bounds if provided
            int? safeRating = rating.HasValue ? Math.Clamp(rating.Value, 1, 5) : null;
            var update = Builders<Booking>.Update
                .Set(x => x.IsCompleted, true)
                .Set(x => x.CompletedAtUtc, DateTime.UtcNow)
                .Set(x => x.UserRating, safeRating)
                .Set(x => x.UserFeedback, feedback);
            var res = await _bookings.UpdateOneAsync(x => x.Id == id && x.UserId == userId, update);
            return res.ModifiedCount == 1;
        }
    }
}