using Microsoft.Extensions.Configuration;
using Microsoft.AspNetCore.Identity;
using MongoDB.Driver;
using ServConnect.Models;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace ServConnect.Services
{
    public class OrderService : IOrderService
    {
        private readonly IMongoCollection<Order> _orders;
        private readonly IMongoCollection<Item> _items;
        private readonly IItemService _itemService;
        private readonly IConfiguration _config;
        private readonly IHttpClientFactory _httpFactory;
        private readonly ISmsService _sms;
        private readonly UserManager<Users> _userManager;

        public OrderService(IConfiguration config, IItemService itemService, IHttpClientFactory httpFactory, ISmsService sms, UserManager<Users> userManager)
        {
            _config = config;
            _httpFactory = httpFactory;
            _sms = sms;
            _userManager = userManager;
            var conn = config["MongoDB:ConnectionString"] ?? "mongodb://localhost:27017";
            var dbName = config["MongoDB:DatabaseName"] ?? "ServConnectDb";
            var client = new MongoClient(conn);
            var db = client.GetDatabase(dbName);
            _orders = db.GetCollection<Order>("Orders");
            _items = db.GetCollection<Item>("Items");
            _itemService = itemService;
        }

        public async Task<(Order order, string razorpayOrderId)> CreateOrderWithAddressAsync(Guid userId, string userEmail, string itemId, int quantity, UserAddress address, string? userAddressId = null)
        {
            if (quantity <= 0) throw new ArgumentException("Quantity must be > 0");
            var item = await _items.Find(i => i.Id == itemId && i.IsActive).FirstOrDefaultAsync();
            if (item == null) throw new InvalidOperationException("Item not found");
            if (quantity > item.Stock) throw new InvalidOperationException("Insufficient stock");

            var total = item.Price * quantity;
            var amountPaise = (long)(total * 100);

            // Create Razorpay order
            var razorpayOrderId = await CreateRazorpayOrderAsync(amountPaise);

            var order = new Order
            {
                Id = null!,
                UserId = userId,
                UserEmail = userEmail,
                VendorId = item.OwnerId,
                ItemId = item.Id!,
                ItemTitle = item.Title,
                ItemPrice = item.Price,
                Quantity = quantity,
                TotalAmount = total,
                
                // Legacy field for backward compatibility
                ShippingAddress = address.FormattedAddress,
                
                // Detailed address fields
                ShippingFullName = address.FullName,
                ShippingPhoneNumber = address.PhoneNumber,
                ShippingAddressLine1 = address.AddressLine1,
                ShippingAddressLine2 = address.AddressLine2,
                ShippingCity = address.City,
                ShippingState = address.State,
                ShippingPostalCode = address.PostalCode,
                ShippingCountry = address.Country,
                ShippingLandmark = address.Landmark,
                UserAddressId = userAddressId,
                
                Status = OrderStatus.Pending,
                RazorpayOrderId = razorpayOrderId,
                PaymentStatus = PaymentStatus.Created,
                CreatedAtUtc = DateTime.UtcNow,
                UpdatedAtUtc = DateTime.UtcNow
            };

            await _orders.InsertOneAsync(order);
            return (order, razorpayOrderId);
        }

        public async Task<(Order order, string razorpayOrderId)> CreateOrderAsync(Guid userId, string userEmail, string itemId, int quantity, string shippingAddress)
        {
            if (quantity <= 0) throw new ArgumentException("Quantity must be > 0");
            var item = await _items.Find(i => i.Id == itemId && i.IsActive).FirstOrDefaultAsync();
            if (item == null) throw new InvalidOperationException("Item not found");
            if (quantity > item.Stock) throw new InvalidOperationException("Insufficient stock");

            var total = item.Price * quantity;
            var amountPaise = (long)(total * 100);

            // Create Razorpay order
            var razorpayOrderId = await CreateRazorpayOrderAsync(amountPaise);

            var order = new Order
            {
                Id = null!,
                UserId = userId,
                UserEmail = userEmail,
                VendorId = item.OwnerId,
                ItemId = item.Id!,
                ItemTitle = item.Title,
                ItemPrice = item.Price,
                Quantity = quantity,
                TotalAmount = total,
                ShippingAddress = shippingAddress,
                Status = OrderStatus.Pending,
                RazorpayOrderId = razorpayOrderId,
                PaymentStatus = PaymentStatus.Created,
                CreatedAtUtc = DateTime.UtcNow,
                UpdatedAtUtc = DateTime.UtcNow
            };

            await _orders.InsertOneAsync(order);
            return (order, razorpayOrderId);
        }

        public async Task<bool> VerifyPaymentAndConfirmAsync(string orderId, string razorpayOrderId, string razorpayPaymentId, string razorpaySignature)
        {
            var order = await _orders.Find(o => o.Id == orderId).FirstOrDefaultAsync();
            if (order == null) return false;
            if (order.RazorpayOrderId != razorpayOrderId) return false;

            var secret = _config["Razorpay:Secret"] ?? string.Empty;
            if (string.IsNullOrEmpty(secret)) return false;

            var payload = $"{razorpayOrderId}|{razorpayPaymentId}";
            using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(secret));
            var hash = hmac.ComputeHash(Encoding.UTF8.GetBytes(payload));
            var expectedSignature = BitConverter.ToString(hash).Replace("-", string.Empty).ToLowerInvariant();
            var ok = expectedSignature == (razorpaySignature ?? string.Empty);
            if (!ok)
            {
                var failUpdate = Builders<Order>.Update
                    .Set(x => x.PaymentStatus, PaymentStatus.Failed)
                    .Set(x => x.RazorpayPaymentId, razorpayPaymentId)
                    .Set(x => x.UpdatedAtUtc, DateTime.UtcNow);
                await _orders.UpdateOneAsync(x => x.Id == orderId, failUpdate);
                return false;
            }

            var reduced = await _itemService.ReduceStockAsync(order.ItemId, order.Quantity);
            if (!reduced)
            {
                var invUpdate = Builders<Order>.Update
                    .Set(x => x.PaymentStatus, PaymentStatus.Failed)
                    .Set(x => x.RazorpayPaymentId, razorpayPaymentId)
                    .Set(x => x.UpdatedAtUtc, DateTime.UtcNow);
                await _orders.UpdateOneAsync(x => x.Id == orderId, invUpdate);
                return false;
            }

            var update = Builders<Order>.Update
                .Set(x => x.RazorpayPaymentId, razorpayPaymentId)
                .Set(x => x.PaymentStatus, PaymentStatus.Paid)
                .Set(x => x.Status, OrderStatus.Pending)
                .Set(x => x.UpdatedAtUtc, DateTime.UtcNow);
            var res = await _orders.UpdateOneAsync(x => x.Id == orderId, update);

            if (res.ModifiedCount == 1)
            {
                var user = await _userManager.FindByIdAsync(order.UserId.ToString());
                var vendor = await _userManager.FindByIdAsync(order.VendorId.ToString());
                var userMsg = $"Payment received for order {order.Id}. Status: Pending. We'll notify when shipped.";
                var vendorMsg = $"New paid order {order.Id} for item '{order.ItemTitle}' x{order.Quantity}. Please ship soon.";
                if (!string.IsNullOrWhiteSpace(user?.PhoneNumber)) _ = _sms.SendSmsAsync(user.PhoneNumber!, userMsg);
                if (!string.IsNullOrWhiteSpace(vendor?.PhoneNumber)) _ = _sms.SendSmsAsync(vendor.PhoneNumber!, vendorMsg);
            }

            return res.ModifiedCount == 1;
        }

        public async Task<bool> MarkShippedAsync(string orderId, Guid vendorId, string? trackingUrl = null)
        {
            var update = Builders<Order>.Update
                .Set(x => x.Status, OrderStatus.Shipped)
                .Set(x => x.TrackingUrl, trackingUrl)
                .Set(x => x.UpdatedAtUtc, DateTime.UtcNow);
            var res = await _orders.UpdateOneAsync(x => x.Id == orderId && x.VendorId == vendorId && x.PaymentStatus == PaymentStatus.Paid, update);

            if (res.ModifiedCount == 1)
            {
                var ord = await GetByIdAsync(orderId);
                if (ord != null)
                {
                    var user = await _userManager.FindByIdAsync(ord.UserId.ToString());
                    var vendor = await _userManager.FindByIdAsync(ord.VendorId.ToString());
                    var msgUser = $"Your order {ord.Id} has been shipped." + (string.IsNullOrWhiteSpace(ord.TrackingUrl) ? string.Empty : $" Track: {ord.TrackingUrl}");
                    var msgVendor = $"Order {ord.Id} marked shipped.";
                    if (!string.IsNullOrWhiteSpace(user?.PhoneNumber)) _ = _sms.SendSmsAsync(user.PhoneNumber!, msgUser);
                    if (!string.IsNullOrWhiteSpace(vendor?.PhoneNumber)) _ = _sms.SendSmsAsync(vendor.PhoneNumber!, msgVendor);
                }
            }
            return res.ModifiedCount == 1;
        }

        public async Task<bool> SetTrackingUrlAsync(string orderId, Guid vendorId, string trackingUrl)
        {
            if (string.IsNullOrWhiteSpace(trackingUrl)) return false;
            var update = Builders<Order>.Update
                .Set(x => x.TrackingUrl, trackingUrl)
                .Set(x => x.UpdatedAtUtc, DateTime.UtcNow);
            var res = await _orders.UpdateOneAsync(x => x.Id == orderId && x.VendorId == vendorId, update);
            if (res.ModifiedCount == 1)
            {
                var ord = await GetByIdAsync(orderId);
                if (ord != null)
                {
                    var user = await _userManager.FindByIdAsync(ord.UserId.ToString());
                    var msgUser = $"Tracking updated for order {ord.Id}. Track here: {trackingUrl}";
                    if (!string.IsNullOrWhiteSpace(user?.PhoneNumber)) _ = _sms.SendSmsAsync(user.PhoneNumber!, msgUser);
                }
            }
            return res.ModifiedCount == 1;
        }

        public async Task<bool> MarkDeliveredAsync(string orderId, Guid userId)
        {
            var update = Builders<Order>.Update
                .Set(x => x.Status, OrderStatus.Delivered)
                .Set(x => x.UpdatedAtUtc, DateTime.UtcNow);
            var res = await _orders.UpdateOneAsync(x => x.Id == orderId && x.UserId == userId && x.PaymentStatus == PaymentStatus.Paid, update);
            if (res.ModifiedCount == 1)
            {
                var ord = await GetByIdAsync(orderId);
                if (ord != null)
                {
                    var user = await _userManager.FindByIdAsync(ord.UserId.ToString());
                    var vendor = await _userManager.FindByIdAsync(ord.VendorId.ToString());
                    var msgUser = $"Order {ord.Id} delivered. Thanks for shopping!";
                    var msgVendor = $"Order {ord.Id} confirmed delivered.";
                    if (!string.IsNullOrWhiteSpace(user?.PhoneNumber)) _ = _sms.SendSmsAsync(user.PhoneNumber!, msgUser);
                    if (!string.IsNullOrWhiteSpace(vendor?.PhoneNumber)) _ = _sms.SendSmsAsync(vendor.PhoneNumber!, msgVendor);
                }
            }
            return res.ModifiedCount == 1;
        }

        public async Task<bool> CancelAsync(string orderId, Guid actorId, bool isVendor)
        {
            var filter = Builders<Order>.Filter.Eq(x => x.Id, orderId) &
                         (isVendor ? Builders<Order>.Filter.Eq(x => x.VendorId, actorId) : Builders<Order>.Filter.Eq(x => x.UserId, actorId));
            var current = await _orders.Find(filter).FirstOrDefaultAsync();
            if (current == null) return false;
            if (current.Status == OrderStatus.Shipped || current.Status == OrderStatus.OutForDelivery || current.Status == OrderStatus.Delivered) return false;

            var update = Builders<Order>.Update
                .Set(x => x.Status, OrderStatus.Cancelled)
                .Set(x => x.UpdatedAtUtc, DateTime.UtcNow);
            var res = await _orders.UpdateOneAsync(x => x.Id == orderId, update);

            if (res.ModifiedCount == 1 && current.PaymentStatus == PaymentStatus.Paid)
            {
                await _itemService.IncreaseStockAsync(current.ItemId, current.Quantity);
                var user = await _userManager.FindByIdAsync(current.UserId.ToString());
                var vendor = await _userManager.FindByIdAsync(current.VendorId.ToString());
                var msgUser = $"Order {current.Id} cancelled. Amount will be refunded as per policy.";
                var msgVendor = $"Order {current.Id} cancelled. Stock adjusted.";
                if (!string.IsNullOrWhiteSpace(user?.PhoneNumber)) _ = _sms.SendSmsAsync(user.PhoneNumber!, msgUser);
                if (!string.IsNullOrWhiteSpace(vendor?.PhoneNumber)) _ = _sms.SendSmsAsync(vendor.PhoneNumber!, msgVendor);
            }

            return res.ModifiedCount == 1;
        }

        public async Task<List<Order>> GetOrdersForVendorAsync(Guid vendorId)
        {
            return await _orders.Find(o => o.VendorId == vendorId).SortByDescending(o => o.CreatedAtUtc).ToListAsync();
        }

        public async Task<List<Order>> GetOrdersForUserAsync(Guid userId)
        {
            return await _orders.Find(o => o.UserId == userId).SortByDescending(o => o.CreatedAtUtc).ToListAsync();
        }

        public async Task<Order?> GetByIdAsync(string id)
        {
            return await _orders.Find(o => o.Id == id).FirstOrDefaultAsync();
        }

        // New status management methods
        public async Task<bool> AcceptOrderAsync(string orderId, Guid vendorId)
        {
            return await UpdateOrderStatusAsync(orderId, vendorId, OrderStatus.Accepted);
        }

        public async Task<bool> MarkPackedAsync(string orderId, Guid vendorId)
        {
            return await UpdateOrderStatusAsync(orderId, vendorId, OrderStatus.Packed);
        }

        public async Task<bool> MarkOutForDeliveryAsync(string orderId, Guid vendorId)
        {
            return await UpdateOrderStatusAsync(orderId, vendorId, OrderStatus.OutForDelivery);
        }

        public async Task<bool> UpdateOrderStatusAsync(string orderId, Guid vendorId, OrderStatus status, string? trackingUrl = null)
        {
            var updateBuilder = Builders<Order>.Update
                .Set(x => x.Status, status)
                .Set(x => x.UpdatedAtUtc, DateTime.UtcNow);

            if (!string.IsNullOrEmpty(trackingUrl))
            {
                updateBuilder = updateBuilder.Set(x => x.TrackingUrl, trackingUrl);
            }

            var res = await _orders.UpdateOneAsync(
                x => x.Id == orderId && x.VendorId == vendorId && x.PaymentStatus == PaymentStatus.Paid,
                updateBuilder
            );

            if (res.ModifiedCount == 1)
            {
                var ord = await GetByIdAsync(orderId);
                if (ord != null)
                {
                    await SendStatusUpdateNotifications(ord, status);
                }
            }

            return res.ModifiedCount == 1;
        }

        private async Task SendStatusUpdateNotifications(Order order, OrderStatus status)
        {
            var user = await _userManager.FindByIdAsync(order.UserId.ToString());
            var vendor = await _userManager.FindByIdAsync(order.VendorId.ToString());

            string userMsg = status switch
            {
                OrderStatus.Accepted => $"Your order {order.Id} has been accepted by the vendor.",
                OrderStatus.Packed => $"Your order {order.Id} has been packed and is ready for shipment.",
                OrderStatus.Shipped => $"Your order {order.Id} has been shipped." + (string.IsNullOrWhiteSpace(order.TrackingUrl) ? "" : $" Track: {order.TrackingUrl}"),
                OrderStatus.OutForDelivery => $"Your order {order.Id} is out for delivery. You should receive it soon!",
                _ => $"Your order {order.Id} status updated to {status}."
            };

            string vendorMsg = status switch
            {
                OrderStatus.Accepted => $"Order {order.Id} accepted.",
                OrderStatus.Packed => $"Order {order.Id} marked as packed.",
                OrderStatus.Shipped => $"Order {order.Id} marked as shipped.",
                OrderStatus.OutForDelivery => $"Order {order.Id} marked as out for delivery.",
                _ => $"Order {order.Id} status updated to {status}."
            };

            if (!string.IsNullOrWhiteSpace(user?.PhoneNumber)) _ = _sms.SendSmsAsync(user.PhoneNumber!, userMsg);
            if (!string.IsNullOrWhiteSpace(vendor?.PhoneNumber)) _ = _sms.SendSmsAsync(vendor.PhoneNumber!, vendorMsg);
        }

        private async Task<string> CreateRazorpayOrderAsync(long amountPaise)
        {
            var keyId = _config["Razorpay:KeyId"] ?? string.Empty;
            var secret = _config["Razorpay:Secret"] ?? string.Empty;
            if (string.IsNullOrEmpty(keyId) || string.IsNullOrEmpty(secret))
                throw new InvalidOperationException("Razorpay configuration missing");

            var http = _httpFactory.CreateClient();
            http.BaseAddress = new Uri("https://api.razorpay.com/");
            var authToken = Convert.ToBase64String(Encoding.UTF8.GetBytes($"{keyId}:{secret}"));
            http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", authToken);

            var payload = new
            {
                amount = amountPaise,
                currency = "INR",
                receipt = $"rcpt_{Guid.NewGuid():N}",
                payment_capture = 1
            };
            var content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");
            var resp = await http.PostAsync("v1/orders", content);
            if (!resp.IsSuccessStatusCode)
            {
                var err = await resp.Content.ReadAsStringAsync();
                throw new InvalidOperationException($"Failed to create Razorpay order: {resp.StatusCode} {err}");
            }
            using var stream = await resp.Content.ReadAsStreamAsync();
            using var doc = await JsonDocument.ParseAsync(stream);
            return doc.RootElement.GetProperty("id").GetString() ?? string.Empty;
        }
    }
}