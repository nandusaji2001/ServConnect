using GasMonitorDashboard.Models;
using System.Collections.Concurrent;

namespace GasMonitorDashboard.Services;

public class InMemoryWeightReadingService : IWeightReadingService
{
    private readonly ConcurrentBag<WeightReading> _readings = new();
    private int _nextId = 1;

    public Task<WeightReadingResponse> AddReadingAsync(WeightReadingRequest request)
    {
        var reading = new WeightReading
        {
            Id = Interlocked.Increment(ref _nextId),
            Weight = request.Weight,
            DeviceId = request.DeviceId ?? "ESP32-001",
            CylinderId = request.CylinderId ?? "cylinder_01",
            RawReading = request.RawReading,
            BatteryLevel = request.BatteryLevel,
            Timestamp = DateTime.UtcNow,
            Status = DetermineStatus(request.Weight)
        };

        _readings.Add(reading);

        return Task.FromResult(new WeightReadingResponse
        {
            Success = true,
            Message = "Reading added successfully",
            Data = reading
        });
    }

    public Task<List<WeightReading>> GetReadingsAsync(int limit = 100)
    {
        var readings = _readings
            .OrderByDescending(r => r.Timestamp)
            .Take(limit)
            .ToList();

        return Task.FromResult(readings);
    }

    public Task<List<WeightReading>> GetAllReadingsAsync()
    {
        var readings = _readings
            .OrderByDescending(r => r.Timestamp)
            .ToList();

        return Task.FromResult(readings);
    }

    public Task<List<WeightReading>> GetRecentReadingsAsync(int count = 10)
    {
        var readings = _readings
            .OrderByDescending(r => r.Timestamp)
            .Take(count)
            .ToList();

        return Task.FromResult(readings);
    }

    public Task<List<WeightReading>> GetReadingsByDateRangeAsync(DateTime startDate, DateTime endDate)
    {
        var readings = _readings
            .Where(r => r.Timestamp >= startDate && r.Timestamp <= endDate)
            .OrderByDescending(r => r.Timestamp)
            .ToList();

        return Task.FromResult(readings);
    }

    public Task<DashboardStats> GetDashboardStatsAsync()
    {
        var readings = _readings.ToList();
        
        if (!readings.Any())
        {
            return Task.FromResult(new DashboardStats
            {
                CurrentWeight = 0,
                AverageWeight = 0,
                MaxWeight = 0,
                MinWeight = 0,
                TotalReadings = 0,
                LastReading = DateTime.MinValue,
                Status = "No Data",
                RecentReadings = new List<WeightReading>()
            });
        }

        var weights = readings.Select(r => r.Weight).ToList();
        var latest = readings.OrderByDescending(r => r.Timestamp).First();

        return Task.FromResult(new DashboardStats
        {
            CurrentWeight = latest.Weight,
            AverageWeight = weights.Average(),
            MaxWeight = weights.Max(),
            MinWeight = weights.Min(),
            TotalReadings = readings.Count,
            LastReading = latest.Timestamp,
            Status = latest.Status,
            RecentReadings = readings.OrderByDescending(r => r.Timestamp).Take(10).ToList()
        });
    }

    public Task<WeightReading?> GetLatestReadingAsync()
    {
        var latest = _readings
            .OrderByDescending(r => r.Timestamp)
            .FirstOrDefault();

        return Task.FromResult(latest);
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
