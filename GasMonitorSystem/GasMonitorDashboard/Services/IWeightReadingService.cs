using GasMonitorDashboard.Models;

namespace GasMonitorDashboard.Services;

public interface IWeightReadingService
{
    Task<WeightReadingResponse> AddReadingAsync(WeightReadingRequest request);
    Task<List<WeightReading>> GetReadingsAsync(int limit = 100);
    Task<List<WeightReading>> GetAllReadingsAsync();
    Task<List<WeightReading>> GetRecentReadingsAsync(int count = 10);
    Task<List<WeightReading>> GetReadingsByDateRangeAsync(DateTime startDate, DateTime endDate);
    Task<DashboardStats> GetDashboardStatsAsync();
    Task<WeightReading?> GetLatestReadingAsync();
}
