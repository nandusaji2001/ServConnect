using MongoDB.Driver;
using ServConnect.Models;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace ServConnect.Services
{
    public class BookingPaymentService : IBookingPaymentService
    {
        private readonly IMongoCollection<BookingPayment> _payments;
        private readonly IConfiguration _configuration;
        private readonly IHttpClientFactory _httpClientFactory;

        public BookingPaymentService(IConfiguration configuration, IHttpClientFactory httpClientFactory)
        {
            var conn = configuration["MongoDB:ConnectionString"] ?? "mongodb://localhost:27017";
            var dbName = configuration["MongoDB:DatabaseName"] ?? "ServConnectDb";
            var client = new MongoClient(conn);
            var db = client.GetDatabase(dbName);
            _payments = db.GetCollection<BookingPayment>("BookingPayments");
            
            _configuration = configuration;
            _httpClientFactory = httpClientFactory;
        }

        public async Task<BookingPayment> CreatePaymentAsync(Guid userId, string userName, string userEmail, 
            string bookingId, string serviceName, string providerName, decimal amountInRupees, 
            int? userRating, string? userFeedback)
        {
            var payment = new BookingPayment
            {
                UserId = userId,
                UserName = userName,
                UserEmail = userEmail,
                BookingId = bookingId,
                ServiceName = serviceName,
                ProviderName = providerName,
                AmountInRupees = amountInRupees,
                ServiceCompletedAt = DateTime.UtcNow,
                UserRating = userRating,
                UserFeedback = userFeedback,
                Status = BookingPaymentStatus.Pending
            };

            // Create Razorpay order
            try
            {
                var orderId = await CreateRazorpayOrderAsync(payment.AmountInPaise, payment.Currency, $"booking_{bookingId}_{DateTime.UtcNow.Ticks}");
                payment.RazorpayOrderId = orderId;
            }
            catch (Exception ex)
            {
                // Log error but continue - we can create the order later
                Console.WriteLine($"Failed to create Razorpay order: {ex.Message}");
            }

            await _payments.InsertOneAsync(payment);
            return payment;
        }

        public async Task<BookingPayment?> GetPaymentByIdAsync(string paymentId)
        {
            return await _payments.Find(p => p.Id == paymentId).FirstOrDefaultAsync();
        }

        public async Task<BookingPayment?> GetPaymentByBookingIdAsync(string bookingId)
        {
            return await _payments.Find(p => p.BookingId == bookingId).FirstOrDefaultAsync();
        }

        public async Task<bool> MarkPaymentPaidAsync(string paymentId, string razorpayOrderId, string razorpayPaymentId, string razorpaySignature)
        {
            var filter = Builders<BookingPayment>.Filter.Eq(p => p.Id, paymentId);
            var update = Builders<BookingPayment>.Update
                .Set(p => p.Status, BookingPaymentStatus.Paid)
                .Set(p => p.RazorpayOrderId, razorpayOrderId)
                .Set(p => p.RazorpayPaymentId, razorpayPaymentId)
                .Set(p => p.RazorpaySignature, razorpaySignature)
                .Set(p => p.UpdatedAt, DateTime.UtcNow);

            var result = await _payments.UpdateOneAsync(filter, update);
            return result.ModifiedCount > 0;
        }

        public async Task<List<BookingPayment>> GetPaymentsByUserAsync(Guid userId)
        {
            return await _payments.Find(p => p.UserId == userId)
                .SortByDescending(p => p.CreatedAt)
                .ToListAsync();
        }

        public async Task<bool> HasPendingPaymentsAsync(Guid userId)
        {
            var count = await _payments.CountDocumentsAsync(p => p.UserId == userId && p.Status == BookingPaymentStatus.Pending);
            return count > 0;
        }

        public async Task<List<BookingPayment>> GetPendingPaymentsByUserAsync(Guid userId)
        {
            return await _payments.Find(p => p.UserId == userId && p.Status == BookingPaymentStatus.Pending)
                .SortByDescending(p => p.CreatedAt)
                .ToListAsync();
        }

        private async Task<string?> CreateRazorpayOrderAsync(int amountInPaise, string currency, string receipt)
        {
            try
            {
                var razorpayKey = _configuration["Razorpay:KeyId"];
                var razorpaySecret = _configuration["Razorpay:KeySecret"];

                if (string.IsNullOrEmpty(razorpayKey) || string.IsNullOrEmpty(razorpaySecret))
                {
                    throw new InvalidOperationException("Razorpay credentials not configured");
                }

                var client = _httpClientFactory.CreateClient();
                var credentials = Convert.ToBase64String(Encoding.UTF8.GetBytes($"{razorpayKey}:{razorpaySecret}"));
                client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Basic", credentials);

                var orderData = new
                {
                    amount = amountInPaise,
                    currency = currency,
                    receipt = receipt
                };

                var json = JsonSerializer.Serialize(orderData);
                var content = new StringContent(json, Encoding.UTF8, "application/json");

                var response = await client.PostAsync("https://api.razorpay.com/v1/orders", content);
                
                if (response.IsSuccessStatusCode)
                {
                    var responseJson = await response.Content.ReadAsStringAsync();
                    var orderResponse = JsonSerializer.Deserialize<JsonElement>(responseJson);
                    return orderResponse.GetProperty("id").GetString();
                }
                else
                {
                    var errorContent = await response.Content.ReadAsStringAsync();
                    throw new HttpRequestException($"Razorpay API error: {response.StatusCode} - {errorContent}");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error creating Razorpay order: {ex.Message}");
                throw;
            }
        }
    }
}
