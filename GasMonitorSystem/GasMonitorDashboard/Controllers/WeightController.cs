using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using GasMonitorDashboard.Models;
using GasMonitorDashboard.Services;
using GasMonitorDashboard.Hubs;

namespace GasMonitorDashboard.Controllers;

[ApiController]
[Route("api/[controller]")]
public class WeightController : ControllerBase
{
    private readonly IWeightReadingService _weightService;
    private readonly IHubContext<WeightHub> _hubContext;
    private readonly ILogger<WeightController> _logger;

    public WeightController(
        IWeightReadingService weightService, 
        IHubContext<WeightHub> hubContext,
        ILogger<WeightController> logger)
    {
        _weightService = weightService;
        _hubContext = hubContext;
        _logger = logger;
    }

    [HttpGet("test")]
    public IActionResult TestEndpoint()
    {
        _logger.LogInformation("Test endpoint called - API is working!");
        return Ok(new { message = "API is working!", timestamp = DateTime.UtcNow });
    }

    [HttpPost("add-test-data")]
    public async Task<IActionResult> AddTestData()
    {
        _logger.LogInformation("Adding test data...");
        
        var random = new Random();
        for (int i = 0; i < 10; i++)
        {
            var testReading = new WeightReadingRequest
            {
                Weight = Math.Round(random.NextDouble() * 15 + 1, 2), // Random weight between 1-16 kg
                DeviceId = "ESP32-TEST"
            };
            
            await _weightService.AddReadingAsync(testReading);
            await Task.Delay(100); // Small delay between readings
        }
        
        var stats = await _weightService.GetDashboardStatsAsync();
        await _hubContext.Clients.All.SendAsync("ReceiveStatsUpdate", stats);
        
        return Ok(new { message = "Test data added successfully", count = 10 });
    }

    [HttpPost("simple")]
    public async Task<ActionResult<WeightReadingResponse>> AddSimpleReading([FromBody] WeightReadingRequest request)
    {
        try
        {
            _logger.LogInformation("=== RECEIVED WEIGHT READING ===");
            _logger.LogInformation("Weight: {Weight} kg, DeviceId: {DeviceId}", request.Weight, request.DeviceId ?? "null");
            
            var response = await _weightService.AddReadingAsync(request);
            var stats = await _weightService.GetDashboardStatsAsync();

            // Send real-time updates via SignalR
            await _hubContext.Clients.All.SendAsync("ReceiveWeightUpdate", response.Data);
            await _hubContext.Clients.All.SendAsync("ReceiveStatsUpdate", stats);

            return Ok(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing weight reading");
            return BadRequest(new WeightReadingResponse
            {
                Success = false,
                Message = $"Error: {ex.Message}"
            });
        }
    }

    [HttpPost]
    public async Task<ActionResult<WeightReadingResponse>> AddReading([FromBody] WeightReadingRequest request)
    {
        return await AddSimpleReading(request);
    }

    [HttpGet]
    public async Task<ActionResult<List<WeightReading>>> GetAllReadings()
    {
        var readings = await _weightService.GetAllReadingsAsync();
        return Ok(readings);
    }

    [HttpGet("recent")]
    public async Task<ActionResult<List<WeightReading>>> GetRecentReadings([FromQuery] int count = 50)
    {
        var readings = await _weightService.GetRecentReadingsAsync(count);
        return Ok(readings);
    }

    [HttpGet("stats")]
    public async Task<ActionResult<DashboardStats>> GetStats()
    {
        var stats = await _weightService.GetDashboardStatsAsync();
        return Ok(stats);
    }

    [HttpGet("range")]
    public async Task<ActionResult<List<WeightReading>>> GetReadingsByDateRange(
        [FromQuery] DateTime startDate, 
        [FromQuery] DateTime endDate)
    {
        var readings = await _weightService.GetReadingsByDateRangeAsync(startDate, endDate);
        return Ok(readings);
    }
}
