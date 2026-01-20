using GasMonitorDashboard.Models;

namespace GasMonitorDashboard.Services;

public class WeightReadingService : IWeightReadingService
{
    private readonly MongoDBService _mongoDBService;

    public WeightReadingService(MongoDBService mongoDBService)
    {
        _mongoDBService = mongoDBService;
    }

    public async Task<WeightReadingResponse> AddReadingAsync(WeightReadingRequest request)
    {
        try
        {
            var reading = new WeightReading
            {
                Weight = request.Weight,
                DeviceId = request.DeviceId ?? "ESP32-001",
                CylinderId = request.CylinderId ?? "cylinder_01",
                RawReading = request.RawReading,
                BatteryLevel = request.BatteryLevel,
                Timestamp = DateTime.UtcNow,
                Status = DetermineStatus(request.Weight)
            };

            await _mongoDBService.CreateAsync(reading);

            return new WeightReadingResponse
            {
                Success = true,
                Message = "Reading added successfully",
                Data = reading
            };
        }
        catch (Exception ex)
        {
            return new WeightReadingResponse
            {
                Success = false,
                Message = $"Error adding reading: {ex.Message}"
            };
        }
    }

    public async Task<List<WeightReading>> GetReadingsAsync(int limit = 100)
    {
        var readings = await _mongoDBService.GetAsync();
        return readings
            .OrderByDescending(r => r.Timestamp)
            .Take(limit)
            .ToList();
    }

    public async Task<List<WeightReading>> GetAllReadingsAsync()
    {
        var readings = await _mongoDBService.GetAsync();
        return readings
            .OrderByDescending(r => r.Timestamp)
            .ToList();
    }

    public async Task<List<WeightReading>> GetRecentReadingsAsync(int count = 10)
    {
        return await _mongoDBService.GetRecentAsync(count);
    }

    public async Task<List<WeightReading>> GetReadingsByDateRangeAsync(DateTime startDate, DateTime endDate)
    {
        var readings = await _mongoDBService.GetAsync();
        return readings
            .Where(r => r.Timestamp >= startDate && r.Timestamp <= endDate)
            .OrderByDescending(r => r.Timestamp)
            .ToList();
    }

    public async Task<DashboardStats> GetDashboardStatsAsync()
    {
        var readings = await _mongoDBService.GetAsync();
        
        if (!readings.Any())
        {
            return new DashboardStats
            {
                CurrentWeight = 0,
                AverageWeight = 0,
                MaxWeight = 0,
                MinWeight = 0,
                TotalReadings = 0,
                LastReading = DateTime.MinValue,
                Status = "No Data",
                RecentReadings = new List<WeightReading>()
            };
        }

        var weights = readings.Select(r => r.Weight).ToList();
        var latest = readings.OrderByDescending(r => r.Timestamp).First();

        return new DashboardStats
        {
            CurrentWeight = latest.Weight,
            AverageWeight = weights.Average(),
            MaxWeight = weights.Max(),
            MinWeight = weights.Min(),
            TotalReadings = readings.Count,
            LastReading = latest.Timestamp,
            Status = latest.Status,
            RecentReadings = readings.OrderByDescending(r => r.Timestamp).Take(10).ToList()
        };
    }

    public async Task<WeightReading?> GetLatestReadingAsync()
    {
        return await _mongoDBService.GetLatestAsync();
    }

    private static string DetermineStatus(double weight)
    {
        return weight switch
        {
            < 1.0 => "Empty",
            < 5.0 => "Low",
            < 10.0 => "Medium",
            _ => "Full"
        };
    }
}
