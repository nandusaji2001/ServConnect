using Microsoft.AspNetCore.SignalR;
using GasMonitorDashboard.Hubs;
using GasMonitorDashboard.Models;

namespace GasMonitorDashboard.Services;

public class SignalRBackgroundService : BackgroundService
{
    private readonly IHubContext<WeightHub> _hubContext;
    private readonly IWeightReadingService _weightReadingService;
    private readonly ILogger<SignalRBackgroundService> _logger;

    public SignalRBackgroundService(
        IHubContext<WeightHub> hubContext,
        IWeightReadingService weightReadingService,
        ILogger<SignalRBackgroundService> logger)
    {
        _hubContext = hubContext;
        _weightReadingService = weightReadingService;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                // Get dashboard stats and broadcast to all clients
                var stats = await _weightReadingService.GetDashboardStatsAsync();
                await _hubContext.Clients.All.SendAsync("UpdateDashboard", stats, stoppingToken);

                // Get latest reading and broadcast
                var latestReading = await _weightReadingService.GetLatestReadingAsync();
                if (latestReading != null)
                {
                    await _hubContext.Clients.All.SendAsync("NewWeightReading", latestReading, stoppingToken);
                }

                _logger.LogInformation("Dashboard stats broadcasted at {Time}", DateTime.UtcNow);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error broadcasting dashboard updates");
            }

            // Wait for 5 seconds before next update
            await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);
        }
    }
}
