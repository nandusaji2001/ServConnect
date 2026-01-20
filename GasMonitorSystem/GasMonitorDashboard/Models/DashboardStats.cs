namespace GasMonitorDashboard.Models;

public class DashboardStats
{
    public double CurrentWeight { get; set; }
    public double AverageWeight { get; set; }
    public double MaxWeight { get; set; }
    public double MinWeight { get; set; }
    public int TotalReadings { get; set; }
    public DateTime LastReading { get; set; }
    public string Status { get; set; } = "Normal";
    public List<WeightReading> RecentReadings { get; set; } = new();
}
