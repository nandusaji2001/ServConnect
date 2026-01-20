namespace GasMonitorDashboard.Models;

public class WeightReading
{
    public int Id { get; set; }
    public double Weight { get; set; }
    public string DeviceId { get; set; } = "ESP32-001";
    public string CylinderId { get; set; } = "cylinder_01";
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    public string Status { get; set; } = "Unknown";
    public long? RawReading { get; set; }
    public int? BatteryLevel { get; set; }
    public double? Temperature { get; set; }
}

public class WeightReadingRequest
{
    public double Weight { get; set; }
    public string? DeviceId { get; set; }
    public string? CylinderId { get; set; }
    public long? RawReading { get; set; }
    public int? BatteryLevel { get; set; }
}

public class WeightReadingResponse
{
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
    public WeightReading? Data { get; set; }
}
