using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using GasMonitorDashboard.Services;
using GasMonitorDashboard.Models;

namespace GasMonitorDashboard.Pages;

public class ReadingsModel : PageModel
{
    private readonly IWeightReadingService _weightService;
    private readonly ILogger<ReadingsModel> _logger;

    public ReadingsModel(IWeightReadingService weightService, ILogger<ReadingsModel> logger)
    {
        _weightService = weightService;
        _logger = logger;
    }

    public List<WeightReading> Readings { get; set; } = new();
    public List<string> AvailableDevices { get; set; } = new();
    
    [BindProperty(SupportsGet = true)]
    public DateTime? StartDate { get; set; }
    
    [BindProperty(SupportsGet = true)]
    public DateTime? EndDate { get; set; }
    
    [BindProperty(SupportsGet = true)]
    public string? DeviceId { get; set; }
    
    [BindProperty(SupportsGet = true)]
    public string? Status { get; set; }

    public async Task OnGetAsync()
    {
        try
        {
            // Get all readings first
            var allReadings = await _weightService.GetAllReadingsAsync();
            
            // Get available devices
            AvailableDevices = allReadings
                .Select(r => r.DeviceId)
                .Distinct()
                .OrderBy(d => d)
                .ToList();

            // Apply filters
            var filteredReadings = allReadings.AsEnumerable();

            if (StartDate.HasValue)
            {
                filteredReadings = filteredReadings.Where(r => r.Timestamp >= StartDate.Value);
            }

            if (EndDate.HasValue)
            {
                filteredReadings = filteredReadings.Where(r => r.Timestamp <= EndDate.Value);
            }

            if (!string.IsNullOrEmpty(DeviceId))
            {
                filteredReadings = filteredReadings.Where(r => r.DeviceId == DeviceId);
            }

            if (!string.IsNullOrEmpty(Status))
            {
                filteredReadings = filteredReadings.Where(r => r.Status == Status);
            }

            Readings = filteredReadings
                .OrderByDescending(r => r.Timestamp)
                .ToList();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading readings");
            Readings = new List<WeightReading>();
            AvailableDevices = new List<string>();
        }
    }
}
