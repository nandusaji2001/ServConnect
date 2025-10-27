using ServConnect.Models;

namespace ServConnect.Services
{
    public interface IRevenueService
    {
        // Revenue tracking
        Task<string> RecordRevenueAsync(RevenueSource revenue);
        Task<List<RevenueSource>> GetRevenueSourcesAsync(DateTime? fromDate = null, DateTime? toDate = null);
        Task<RevenueAnalytics> GetRevenueAnalyticsAsync(DateTime? fromDate = null, DateTime? toDate = null);
        
        // Revenue aggregation
        Task<decimal> GetTotalRevenueAsync();
        Task<decimal> GetRevenueByTypeAsync(RevenueType type, DateTime? fromDate = null, DateTime? toDate = null);
        Task<Dictionary<string, decimal>> GetMonthlyRevenueAsync(int months = 12);
        Task<Dictionary<RevenueType, decimal>> GetRevenueBreakdownAsync(DateTime? fromDate = null, DateTime? toDate = null);
        
        // Data synchronization
        Task SyncRevenueFromPaymentsAsync();
        Task SyncRevenueFromAdvertisementsAsync();
        Task SyncRevenueFromBookingPaymentsAsync();
        
        // ML Predictions
        Task<List<RevenuePrediction>> PredictRevenueAsync(List<int> monthsAhead);
        Task<RevenuePrediction> PredictRevenueForPeriodAsync(int monthsAhead);
        
        // Analytics helpers
        Task<List<RevenueSource>> GetTopRevenueSourcesAsync(int limit = 10);
        Task<decimal> GetAverageMonthlyRevenueAsync(int months = 6);
        Task<double> GetRevenueGrowthRateAsync(int months = 3);
        
        // Detailed transaction breakdown (to show actual source data)
        Task<List<ServicePayment>> GetPaidServicePaymentsAsync(DateTime? fromDate = null, DateTime? toDate = null);
        Task<List<AdvertisementRequest>> GetPaidAdvertisementsAsync(DateTime? fromDate = null, DateTime? toDate = null);
        Task<Dictionary<string, object>> GetDetailedRevenueBreakdownAsync(DateTime? fromDate = null, DateTime? toDate = null);
    }
}
