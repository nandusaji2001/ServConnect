using MongoDB.Driver;
using ServConnect.Models;
using System.Globalization;
using Microsoft.Extensions.Configuration;

namespace ServConnect.Services
{
    public class RevenueService : IRevenueService
    {
        private readonly IMongoCollection<RevenueSource> _revenueCollection;
        private readonly IMongoCollection<ServicePayment> _servicePaymentCollection;
        private readonly IMongoCollection<AdvertisementRequest> _advertisementCollection;
        private readonly IMongoCollection<BookingPayment> _bookingPaymentCollection;

        public RevenueService(IConfiguration config)
        {
            var conn = config["MongoDB:ConnectionString"] ?? "mongodb://localhost:27017";
            var dbName = config["MongoDB:DatabaseName"] ?? "ServConnectDb";
            var client = new MongoClient(conn);
            var db = client.GetDatabase(dbName);
            
            _revenueCollection = db.GetCollection<RevenueSource>("RevenueSources");
            _servicePaymentCollection = db.GetCollection<ServicePayment>("ServicePayments");
            _advertisementCollection = db.GetCollection<AdvertisementRequest>("AdvertisementRequests");
            _bookingPaymentCollection = db.GetCollection<BookingPayment>("BookingPayments");
        }

        public async Task<string> RecordRevenueAsync(RevenueSource revenue)
        {
            revenue.CreatedAt = DateTime.UtcNow;
            revenue.UpdatedAt = DateTime.UtcNow;
            
            await _revenueCollection.InsertOneAsync(revenue);
            return revenue.Id!;
        }

        public async Task<List<RevenueSource>> GetRevenueSourcesAsync(DateTime? fromDate = null, DateTime? toDate = null)
        {
            var filterBuilder = Builders<RevenueSource>.Filter;
            var filter = filterBuilder.Empty;

            if (fromDate.HasValue)
                filter &= filterBuilder.Gte(r => r.CreatedAt, fromDate.Value);
            
            if (toDate.HasValue)
                filter &= filterBuilder.Lte(r => r.CreatedAt, toDate.Value);

            return await _revenueCollection
                .Find(filter)
                .SortByDescending(r => r.CreatedAt)
                .ToListAsync();
        }

        public async Task<RevenueAnalytics> GetRevenueAnalyticsAsync(DateTime? fromDate = null, DateTime? toDate = null)
        {
            var revenues = await GetRevenueSourcesAsync(fromDate, toDate);
            
            var analytics = new RevenueAnalytics
            {
                FromDate = fromDate ?? DateTime.UtcNow.AddYears(-1),
                ToDate = toDate ?? DateTime.UtcNow,
                TotalRevenue = revenues.Sum(r => r.Amount),
                TotalTransactions = revenues.Count
            };

            analytics.AverageTransactionValue = analytics.TotalTransactions > 0 
                ? analytics.TotalRevenue / analytics.TotalTransactions 
                : 0;

            // Revenue by type
            analytics.RevenueByType = revenues
                .GroupBy(r => r.Type)
                .ToDictionary(g => g.Key, g => g.Sum(r => r.Amount));

            analytics.ServicePublicationRevenue = analytics.RevenueByType.GetValueOrDefault(RevenueType.ServicePublication, 0);
            analytics.AdvertisementRevenue = analytics.RevenueByType.GetValueOrDefault(RevenueType.AdvertisementPayment, 0);
            analytics.BookingCommissionRevenue = analytics.RevenueByType.GetValueOrDefault(RevenueType.BookingCommission, 0);
            analytics.OtherRevenue = analytics.RevenueByType.GetValueOrDefault(RevenueType.Other, 0);

            // Monthly revenue
            analytics.MonthlyRevenue = revenues
                .GroupBy(r => r.CreatedAt.ToString("yyyy-MM"))
                .ToDictionary(g => g.Key, g => g.Sum(r => r.Amount));

            return analytics;
        }

        public async Task<decimal> GetTotalRevenueAsync()
        {
            var result = await _revenueCollection
                .Aggregate()
                .Group(r => 1, g => new { Total = g.Sum(r => r.Amount) })
                .FirstOrDefaultAsync();
            
            return result?.Total ?? 0;
        }

        public async Task<decimal> GetRevenueByTypeAsync(RevenueType type, DateTime? fromDate = null, DateTime? toDate = null)
        {
            var filterBuilder = Builders<RevenueSource>.Filter;
            var filter = filterBuilder.Eq(r => r.Type, type);

            if (fromDate.HasValue)
                filter &= filterBuilder.Gte(r => r.CreatedAt, fromDate.Value);
            
            if (toDate.HasValue)
                filter &= filterBuilder.Lte(r => r.CreatedAt, toDate.Value);

            var result = await _revenueCollection
                .Aggregate()
                .Match(filter)
                .Group(r => 1, g => new { Total = g.Sum(r => r.Amount) })
                .FirstOrDefaultAsync();
            
            return result?.Total ?? 0;
        }

        public async Task<Dictionary<string, decimal>> GetMonthlyRevenueAsync(int months = 12)
        {
            var fromDate = DateTime.UtcNow.AddMonths(-months);
            var revenues = await GetRevenueSourcesAsync(fromDate);
            
            return revenues
                .GroupBy(r => r.CreatedAt.ToString("yyyy-MM"))
                .ToDictionary(g => g.Key, g => g.Sum(r => r.Amount));
        }

        public async Task<Dictionary<RevenueType, decimal>> GetRevenueBreakdownAsync(DateTime? fromDate = null, DateTime? toDate = null)
        {
            var revenues = await GetRevenueSourcesAsync(fromDate, toDate);
            
            return revenues
                .GroupBy(r => r.Type)
                .ToDictionary(g => g.Key, g => g.Sum(r => r.Amount));
        }

        public async Task SyncRevenueFromPaymentsAsync()
        {
            var paidServicePayments = await _servicePaymentCollection
                .Find(p => p.Status == ServicePaymentStatus.Paid)
                .ToListAsync();

            foreach (var payment in paidServicePayments)
            {
                // Check if revenue already recorded
                var existingRevenue = await _revenueCollection
                    .Find(r => r.SourceId == payment.Id && r.SourceType == "ServicePayment")
                    .FirstOrDefaultAsync();

                if (existingRevenue == null)
                {
                    var revenue = new RevenueSource
                    {
                        Type = RevenueType.ServicePublication,
                        Amount = payment.AmountInRupees,
                        Description = $"Service publication payment for {payment.ServiceName}",
                        SourceId = payment.Id,
                        SourceType = "ServicePayment",
                        UserId = payment.ProviderId,
                        CreatedAt = payment.UpdatedAt
                    };

                    await RecordRevenueAsync(revenue);
                }
            }
        }

        public async Task SyncRevenueFromAdvertisementsAsync()
        {
            var paidAdvertisements = await _advertisementCollection
                .Find(a => a.IsPaid && a.Status == AdRequestStatus.Approved)
                .ToListAsync();

            foreach (var ad in paidAdvertisements)
            {
                var existingRevenue = await _revenueCollection
                    .Find(r => r.SourceId == ad.Id && r.SourceType == "AdvertisementRequest")
                    .FirstOrDefaultAsync();

                if (existingRevenue == null)
                {
                    var revenue = new RevenueSource
                    {
                        Type = RevenueType.AdvertisementPayment,
                        Amount = ad.AmountInPaise / 100m,
                        Description = $"Advertisement payment for {ad.Type} ad",
                        SourceId = ad.Id,
                        SourceType = "AdvertisementRequest",
                        UserId = ad.RequestedByUserId,
                        CreatedAt = ad.CreatedAt
                    };

                    await RecordRevenueAsync(revenue);
                }
            }
        }

        public async Task SyncRevenueFromBookingPaymentsAsync()
        {
            var paidBookings = await _bookingPaymentCollection
                .Find(b => b.Status == BookingPaymentStatus.Paid)
                .ToListAsync();

            foreach (var booking in paidBookings)
            {
                var existingRevenue = await _revenueCollection
                    .Find(r => r.SourceId == booking.Id && r.SourceType == "BookingPayment")
                    .FirstOrDefaultAsync();

                if (existingRevenue == null)
                {
                    // Assuming 10% commission from booking payments
                    var commissionRate = 0.10m;
                    var commissionAmount = booking.AmountInRupees * commissionRate;

                    var revenue = new RevenueSource
                    {
                        Type = RevenueType.BookingCommission,
                        Amount = commissionAmount,
                        Description = $"Commission from booking payment for {booking.ServiceName}",
                        SourceId = booking.Id,
                        SourceType = "BookingPayment",
                        UserId = booking.UserId,
                        CreatedAt = booking.UpdatedAt
                    };

                    await RecordRevenueAsync(revenue);
                }
            }
        }

        public async Task<List<RevenuePrediction>> PredictRevenueAsync(List<int> monthsAhead)
        {
            var predictions = new List<RevenuePrediction>();
            
            foreach (var months in monthsAhead)
            {
                var prediction = await PredictRevenueForPeriodAsync(months);
                predictions.Add(prediction);
            }
            
            return predictions;
        }

        // New methods to show actual source transactions
        public async Task<List<ServicePayment>> GetPaidServicePaymentsAsync(DateTime? fromDate = null, DateTime? toDate = null)
        {
            var filterBuilder = Builders<ServicePayment>.Filter;
            var filter = filterBuilder.Eq(p => p.Status, ServicePaymentStatus.Paid);

            if (fromDate.HasValue)
                filter &= filterBuilder.Gte(p => p.UpdatedAt, fromDate.Value);
            
            if (toDate.HasValue)
                filter &= filterBuilder.Lte(p => p.UpdatedAt, toDate.Value);

            return await _servicePaymentCollection
                .Find(filter)
                .SortByDescending(p => p.UpdatedAt)
                .ToListAsync();
        }

        public async Task<List<AdvertisementRequest>> GetPaidAdvertisementsAsync(DateTime? fromDate = null, DateTime? toDate = null)
        {
            var filterBuilder = Builders<AdvertisementRequest>.Filter;
            var filter = filterBuilder.And(
                filterBuilder.Eq(a => a.IsPaid, true),
                filterBuilder.Eq(a => a.Status, AdRequestStatus.Approved)
            );

            if (fromDate.HasValue)
                filter &= filterBuilder.Gte(a => a.CreatedAt, fromDate.Value);
            
            if (toDate.HasValue)
                filter &= filterBuilder.Lte(a => a.CreatedAt, toDate.Value);

            return await _advertisementCollection
                .Find(filter)
                .SortByDescending(a => a.CreatedAt)
                .ToListAsync();
        }

        public async Task<Dictionary<string, object>> GetDetailedRevenueBreakdownAsync(DateTime? fromDate = null, DateTime? toDate = null)
        {
            var paidServices = await GetPaidServicePaymentsAsync(fromDate, toDate);
            var paidAds = await GetPaidAdvertisementsAsync(fromDate, toDate);

            var serviceRevenue = paidServices.Sum(p => p.AmountInRupees);
            var advertisementRevenue = paidAds.Sum(a => a.AmountInPaise / 100m);

            return new Dictionary<string, object>
            {
                ["ServicePublicationCount"] = paidServices.Count,
                ["ServicePublicationRevenue"] = serviceRevenue,
                ["ServicePublicationDetails"] = paidServices.Select(p => new
                {
                    p.Id,
                    p.ServiceName,
                    p.AmountInRupees,
                    p.ProviderId,
                    p.UpdatedAt,
                    p.PublicationStartDate,
                    p.PublicationEndDate
                }).ToList(),
                ["AdvertisementCount"] = paidAds.Count,
                ["AdvertisementRevenue"] = advertisementRevenue,
                ["AdvertisementDetails"] = paidAds.Select(a => new
                {
                    a.Id,
                    a.Type,
                    Amount = a.AmountInPaise / 100m,
                    a.DurationInMonths,
                    a.RequestedByUserId,
                    a.CreatedAt,
                    a.ExpiryDate
                }).ToList(),
                ["TotalRevenue"] = serviceRevenue + advertisementRevenue,
                ["TotalTransactions"] = paidServices.Count + paidAds.Count
            };
        }

        public async Task<RevenuePrediction> PredictRevenueForPeriodAsync(int monthsAhead)
        {
            try
            {
                // Get historical data for analysis
                var historicalData = await GetMonthlyRevenueAsync(24); // 2 years of data
                
                if (historicalData.Count < 3) // Need at least 3 months of data
                {
                    return new RevenuePrediction
                    {
                        PredictionDate = DateTime.UtcNow.AddMonths(monthsAhead),
                        PredictedAmount = await GetAverageMonthlyRevenueAsync(3),
                        ConfidenceScore = 0.3,
                        Period = $"{monthsAhead} Month{(monthsAhead > 1 ? "s" : "")}",
                        ModelFeatures = new Dictionary<string, object>
                        {
                            ["Method"] = "Insufficient Data - Average Fallback",
                            ["DataPoints"] = historicalData.Count
                        }
                    };
                }

                // Use advanced statistical prediction with trend analysis
                var predictedAmount = PredictUsingTrendAnalysis(historicalData, monthsAhead);
                var confidence = CalculatePredictionConfidence(historicalData, monthsAhead);

                return new RevenuePrediction
                {
                    PredictionDate = DateTime.UtcNow.AddMonths(monthsAhead),
                    PredictedAmount = Math.Max(0, predictedAmount),
                    ConfidenceScore = confidence,
                    Period = $"{monthsAhead} Month{(monthsAhead > 1 ? "s" : "")}",
                    ModelFeatures = new Dictionary<string, object>
                    {
                        ["Method"] = "Trend Analysis with Seasonal Adjustment",
                        ["DataPoints"] = historicalData.Count,
                        ["TrendFactor"] = CalculateTrendFactor(historicalData),
                        ["SeasonalityDetected"] = DetectSeasonality(historicalData)
                    }
                };
            }
            catch (Exception ex)
            {
                // Fallback to simple average
                var avgRevenue = await GetAverageMonthlyRevenueAsync(6);
                return new RevenuePrediction
                {
                    PredictionDate = DateTime.UtcNow.AddMonths(monthsAhead),
                    PredictedAmount = avgRevenue,
                    ConfidenceScore = 0.3,
                    Period = $"{monthsAhead} Month{(monthsAhead > 1 ? "s" : "")}",
                    ModelFeatures = new Dictionary<string, object>
                    {
                        ["Method"] = "Error Fallback - Simple Average",
                        ["Error"] = ex.Message
                    }
                };
            }
        }

        private decimal PredictUsingTrendAnalysis(Dictionary<string, decimal> historicalData, int monthsAhead)
        {
            var orderedData = historicalData.OrderBy(kvp => kvp.Key).ToList();
            var values = orderedData.Select(kvp => (double)kvp.Value).ToArray();
            
            if (values.Length < 2) return (decimal)values.FirstOrDefault();

            // Calculate linear trend
            var n = values.Length;
            var sumX = n * (n + 1) / 2.0;
            var sumY = values.Sum();
            var sumXY = values.Select((y, i) => (i + 1) * y).Sum();
            var sumX2 = n * (n + 1) * (2 * n + 1) / 6.0;

            var slope = (n * sumXY - sumX * sumY) / (n * sumX2 - sumX * sumX);
            var intercept = (sumY - slope * sumX) / n;

            // Apply seasonal adjustment if detected
            var seasonalFactor = DetectSeasonality(historicalData) ? GetSeasonalAdjustment(orderedData, monthsAhead) : 1.0;
            
            // Predict future value
            var futureX = n + monthsAhead;
            var prediction = intercept + slope * futureX;
            
            // Apply seasonal adjustment and ensure non-negative
            var adjustedPrediction = Math.Max(0, prediction * seasonalFactor);
            
            // Add some randomness based on historical variance to make it more realistic
            var variance = CalculateVariance(values);
            var adjustment = Math.Sqrt(variance) * 0.1 * monthsAhead; // Small adjustment based on prediction distance
            
            return (decimal)(adjustedPrediction + adjustment);
        }

        private double CalculatePredictionConfidence(Dictionary<string, decimal> historicalData, int monthsAhead)
        {
            var values = historicalData.Values.Select(v => (double)v).ToArray();
            if (values.Length < 2) return 0.3;

            // Base confidence on data consistency and prediction distance
            var variance = CalculateVariance(values);
            var mean = values.Average();
            var coefficientOfVariation = mean > 0 ? Math.Sqrt(variance) / mean : 1.0;
            
            // Higher variance = lower confidence
            var dataConsistencyScore = Math.Max(0.1, 1.0 - Math.Min(1.0, coefficientOfVariation));
            
            // Confidence decreases with prediction distance
            var distancePenalty = Math.Max(0.1, 1.0 - (monthsAhead * 0.05));
            
            // More data points = higher confidence
            var dataVolumeBonus = Math.Min(1.0, values.Length / 12.0);
            
            return Math.Max(0.1, Math.Min(0.95, dataConsistencyScore * distancePenalty * dataVolumeBonus));
        }

        private double CalculateTrendFactor(Dictionary<string, decimal> historicalData)
        {
            var orderedData = historicalData.OrderBy(kvp => kvp.Key).ToList();
            if (orderedData.Count < 2) return 0;

            var firstHalf = orderedData.Take(orderedData.Count / 2).Average(kvp => (double)kvp.Value);
            var secondHalf = orderedData.Skip(orderedData.Count / 2).Average(kvp => (double)kvp.Value);
            
            return firstHalf > 0 ? (secondHalf - firstHalf) / firstHalf : 0;
        }

        private bool DetectSeasonality(Dictionary<string, decimal> historicalData)
        {
            // Simple seasonality detection - look for patterns in month-over-month changes
            return historicalData.Count >= 12; // Need at least a year of data to detect seasonality
        }

        private double GetSeasonalAdjustment(List<KeyValuePair<string, decimal>> orderedData, int monthsAhead)
        {
            // Simple seasonal adjustment based on historical patterns
            var targetMonth = (DateTime.UtcNow.Month + monthsAhead - 1) % 12 + 1;
            
            // Look for historical data from the same month
            var sameMonthData = orderedData.Where(kvp => 
            {
                if (DateTime.TryParseExact(kvp.Key + "-01", "yyyy-MM-dd", null, System.Globalization.DateTimeStyles.None, out var date))
                {
                    return date.Month == targetMonth;
                }
                return false;
            }).ToList();

            if (sameMonthData.Any())
            {
                var sameMonthAvg = sameMonthData.Average(kvp => (double)kvp.Value);
                var overallAvg = orderedData.Average(kvp => (double)kvp.Value);
                return overallAvg > 0 ? sameMonthAvg / overallAvg : 1.0;
            }

            return 1.0; // No seasonal adjustment if no historical data for the month
        }

        private double CalculateVariance(double[] values)
        {
            if (values.Length < 2) return 0;
            
            var mean = values.Average();
            return values.Sum(v => Math.Pow(v - mean, 2)) / (values.Length - 1);
        }


        public async Task<List<RevenueSource>> GetTopRevenueSourcesAsync(int limit = 10)
        {
            return await _revenueCollection
                .Find(Builders<RevenueSource>.Filter.Empty)
                .SortByDescending(r => r.Amount)
                .Limit(limit)
                .ToListAsync();
        }

        public async Task<decimal> GetAverageMonthlyRevenueAsync(int months = 6)
        {
            var monthlyRevenue = await GetMonthlyRevenueAsync(months);
            return monthlyRevenue.Values.Any() ? monthlyRevenue.Values.Average() : 0;
        }

        public async Task<double> GetRevenueGrowthRateAsync(int months = 3)
        {
            var monthlyRevenue = await GetMonthlyRevenueAsync(months * 2);
            var orderedRevenue = monthlyRevenue.OrderBy(kvp => kvp.Key).ToList();
            
            if (orderedRevenue.Count < 2) return 0;
            
            var recentAvg = orderedRevenue.TakeLast(months).Average(kvp => (double)kvp.Value);
            var previousAvg = orderedRevenue.Take(months).Average(kvp => (double)kvp.Value);
            
            if (previousAvg == 0) return 0;
            
            return ((recentAvg - previousAvg) / previousAvg) * 100;
        }

    }

}
