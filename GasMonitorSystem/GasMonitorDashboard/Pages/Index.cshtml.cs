using Microsoft.AspNetCore.Mvc.RazorPages;
using GasMonitorDashboard.Services;
using GasMonitorDashboard.Models;

namespace GasMonitorDashboard.Pages;

public class IndexModel : PageModel
{
    private readonly IWeightReadingService _weightService;
    private readonly ILogger<IndexModel> _logger;

    public IndexModel(IWeightReadingService weightService, ILogger<IndexModel> logger)
    {
        _weightService = weightService;
        _logger = logger;
    }

    public DashboardStats Stats { get; set; } = new();
    public List<WeightReading> RecentReadings { get; set; } = new();

    public async Task OnGetAsync()
    {
        try
        {
            Stats = await _weightService.GetDashboardStatsAsync();
            RecentReadings = await _weightService.GetRecentReadingsAsync(20);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading dashboard data");
            Stats = new DashboardStats { Status = "Error" };
            RecentReadings = new List<WeightReading>();
        }
    }
}
