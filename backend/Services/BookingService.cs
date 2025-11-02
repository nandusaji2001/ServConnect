using Microsoft.Extensions.Configuration;
using MongoDB.Driver;
using ServConnect.Models;
using System.Threading.Tasks;

namespace ServConnect.Services
{
    public class BookingService : IBookingService
    {
        private readonly IMongoCollection<Booking> _bookings;
        private readonly IMongoCollection<Users> _users;
        private readonly IMongoCollection<ProviderService> _providerServices;
        private readonly IEmailService _emailService;

        public BookingService(IConfiguration config, IEmailService emailService)
        {
            var conn = config["MongoDB:ConnectionString"] ?? "mongodb://localhost:27017";
            var dbName = config["MongoDB:DatabaseName"] ?? "ServConnectDb";
            var client = new MongoClient(conn);
            var db = client.GetDatabase(dbName);
            _bookings = db.GetCollection<Booking>("Bookings");
            _users = db.GetCollection<Users>("Users");
            _providerServices = db.GetCollection<ProviderService>("ProviderServices");
            _emailService = emailService;
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

            // Send email to provider
            var provider = await _users.Find(u => u.Id == providerId).FirstOrDefaultAsync();
            if (provider?.Email != null)
            {
                var subject = $"New Booking Request for {serviceName}";
                var body = $@"
<html>
<body>
<h2>New Booking Request</h2>
<p>Dear {providerName},</p>
<p>A user has booked your service <strong>{serviceName}</strong>.</p>
<p><strong>User Details:</strong></p>
<ul>
<li>Name: {userName}</li>
<li>Email: {userEmail}</li>
<li>Phone: {contactPhone}</li>
<li>Address: {address}</li>
<li>Requested Date & Time: {serviceDateTime:yyyy-MM-dd HH:mm}</li>
<li>Note: {note ?? "None"}</li>
</ul>
<p>Please log in to your account to accept or reject this booking.</p>
<p>Best regards,<br>ServConnect Team</p>
</body>
</html>";
                await _emailService.SendEmailAsync(provider.Email, subject, body);
            }

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
            if (res.ModifiedCount != 1)
            {
                return false;
            }

            var booking = await _bookings.Find(x => x.Id == id).FirstOrDefaultAsync();

            if (booking != null && status == BookingStatus.Accepted)
            {
                // Automatically reject other pending bookings for the same user, service, and day
                var filterBuilder = Builders<Booking>.Filter;
                var startOfDay = booking.ServiceDateTime.Date;
                var nextDay = startOfDay.AddDays(1);

                var duplicateFilter = filterBuilder.And(
                    filterBuilder.Ne(b => b.Id, booking.Id),
                    filterBuilder.Eq(b => b.UserId, booking.UserId),
                    filterBuilder.Eq(b => b.ServiceName, booking.ServiceName),
                    filterBuilder.Gte(b => b.ServiceDateTime, startOfDay),
                    filterBuilder.Lt(b => b.ServiceDateTime, nextDay),
                    filterBuilder.Eq(b => b.Status, BookingStatus.Pending)
                );

                var duplicateUpdate = Builders<Booking>.Update
                    .Set(b => b.Status, BookingStatus.Rejected)
                    .Set(b => b.ProviderMessage, "Automatically cancelled after another provider accepted this booking.")
                    .Set(b => b.RespondedAtUtc, DateTime.UtcNow);

                await _bookings.UpdateManyAsync(duplicateFilter, duplicateUpdate);

                // Send email to user confirming acceptance
                if (!string.IsNullOrEmpty(booking.UserEmail))
                {
                    var subject = $"Your Booking for {booking.ServiceName} has been Accepted";
                    var body = $@"
<html>
<body>
<h2>Booking Accepted</h2>
<p>Dear {booking.UserName},</p>
<p>Your booking request for <strong>{booking.ServiceName}</strong> with {booking.ProviderName} has been <strong>accepted</strong>.</p>
<p><strong>Booking Details:</strong></p>
<ul>
<li>Service: {booking.ServiceName}</li>
<li>Provider: {booking.ProviderName}</li>
<li>Date & Time: {booking.ServiceDateTime:yyyy-MM-dd HH:mm}</li>
<li>Your Phone: {booking.ContactPhone}</li>
<li>Your Address: {booking.Address}</li>
<li>Note: {booking.Note ?? "None"}</li>
</ul>
<p>If you have any questions, please contact the service provider directly.</p>
<p>Best regards,<br>ServConnect Team</p>
</body>
</html>";
                    await _emailService.SendEmailAsync(booking.UserEmail, subject, body);
                }
            }

            return true;
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

        public async Task<bool> UpdateServiceOtpAsync(string bookingId, string otpId)
        {
            var update = Builders<Booking>.Update.Set(x => x.CurrentOtpId, otpId);
            var res = await _bookings.UpdateOneAsync(x => x.Id == bookingId, update);
            return res.ModifiedCount == 1;
        }

        public async Task<bool> UpdateServiceStatusAsync(string bookingId, Guid providerId, ServiceStatus serviceStatus)
        {
            var updateBuilder = Builders<Booking>.Update.Set(x => x.ServiceStatus, serviceStatus);
            
            // Set appropriate timestamps based on status
            if (serviceStatus == ServiceStatus.InProgress)
            {
                updateBuilder = updateBuilder.Set(x => x.ServiceStartedAt, DateTime.UtcNow);
            }
            else if (serviceStatus == ServiceStatus.Completed)
            {
                updateBuilder = updateBuilder.Set(x => x.ServiceCompletedAt, DateTime.UtcNow);
            }

            var res = await _bookings.UpdateOneAsync(
                x => x.Id == bookingId && x.ProviderId == providerId, 
                updateBuilder
            );
            return res.ModifiedCount == 1;
        }

        public async Task<bool> UpdateProviderAsync(string bookingId, Guid newProviderId, string newProviderName)
        {
            var update = Builders<Booking>.Update
                .Set(x => x.ProviderId, newProviderId)
                .Set(x => x.ProviderName, newProviderName)
                .Set(x => x.ServiceStatus, ServiceStatus.NotStarted) // Reset service status for new provider
                .Set(x => x.ServiceStartedAt, null)
                .Set(x => x.ServiceCompletedAt, null)
                .Set(x => x.CurrentOtpId, null);

            var res = await _bookings.UpdateOneAsync(x => x.Id == bookingId, update);
            return res.ModifiedCount == 1;
        }
    }
}