using HealthMonitorApp.Data;
using HealthMonitorApp.Models;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;

namespace HealthMonitorApp.Services
{
    public class HealthCheckHostedService : IHostedService, IDisposable
    {
        private readonly IHubContext<NotificationHub> _hubContext;
        private readonly ILogger<HealthCheckHostedService> _logger;
        private readonly IServiceScopeFactory _scopeFactory;
        private Timer _timer;

        public HealthCheckHostedService(IServiceScopeFactory scopeFactory, ILogger<HealthCheckHostedService> logger,
            IHubContext<NotificationHub> hubContext)
        {
            _scopeFactory = scopeFactory ?? throw new ArgumentNullException(nameof(scopeFactory));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _hubContext = hubContext ?? throw new ArgumentNullException(nameof(hubContext));
        }

        public void Dispose()
        {
            _timer?.Dispose();
        }

        public Task StartAsync(CancellationToken cancellationToken)
        {
            _logger.LogInformation("Health Check Hosted Service is starting.");

            using (var scope = _scopeFactory.CreateScope())
            {
                var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
                int interval = 30;

                Settings? setting = context.Settings.FirstOrDefault();

                if (setting != null)
                {
                    interval = setting.TestInterval.Minutes;
                }

                _timer = new Timer(_ => Task.Run(DoWork), null, TimeSpan.Zero, TimeSpan.FromMinutes(interval));
            }

            return Task.CompletedTask;
        }

        public Task StopAsync(CancellationToken cancellationToken)
        {
            _logger.LogInformation("Health Check Hosted Service is stopping.");

            _timer?.Change(Timeout.Infinite, 0);

            return Task.CompletedTask;
        }

        private async Task DoWork()
        {
            _logger.LogInformation("Health Check Hosted Service is working.");

            var failedServices = new List<string>();

            using (var scope = _scopeFactory.CreateScope())
            {
                var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
                var healthCheckService = scope.ServiceProvider.GetRequiredService<HealthCheckService>();
                var warningService = scope.ServiceProvider.GetRequiredService<WarningService>();

                var serviceStatusList = await context.ServiceStatuses
                    .Include(apiEndPoints => apiEndPoints.ApiEndpoint)
                    .Include(apiEndPoints => apiEndPoints.ApiEndpoint.ApiGroup)
                    .ToListAsync();

                foreach (var serviceStatus in serviceStatusList)
                {
                    await healthCheckService.CheckServiceStatusHealthAsync(serviceStatus);

                    if (!serviceStatus.IsHealthy) failedServices.Add(serviceStatus.ApiEndpoint.Name);
                }

                if (failedServices.Any())
                {
                    var emailBody = "The following services failed the health check: ";
                    _logger.LogWarning("The following services failed the health check: {Services}", failedServices);
                    foreach (var failedService in failedServices) emailBody += failedService + ", ";

                    await warningService.SendEmailViaExchangeAsync(
                        "Sos Health Check Failure",
                        emailBody
                    );
                }
            }

            await _hubContext.Clients.All.SendAsync("RefreshPage");
        }
    }
}
