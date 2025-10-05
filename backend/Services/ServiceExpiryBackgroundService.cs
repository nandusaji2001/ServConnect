using ServConnect.Services;

namespace ServConnect.BackgroundServices
{
    public class ServiceExpiryBackgroundService : BackgroundService
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly ILogger<ServiceExpiryBackgroundService> _logger;
        private readonly TimeSpan _checkInterval = TimeSpan.FromHours(1); // Check every hour

        public ServiceExpiryBackgroundService(
            IServiceProvider serviceProvider,
            ILogger<ServiceExpiryBackgroundService> logger)
        {
            _serviceProvider = serviceProvider;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("Service Expiry Background Service started");

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    using var scope = _serviceProvider.CreateScope();
                    var paymentService = scope.ServiceProvider.GetRequiredService<IServicePaymentService>();
                    
                    await paymentService.DisableExpiredServicesAsync();
                    _logger.LogInformation("Checked for expired services at {Time}", DateTime.UtcNow);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error occurred while checking for expired services");
                }

                await Task.Delay(_checkInterval, stoppingToken);
            }
        }
    }
}
