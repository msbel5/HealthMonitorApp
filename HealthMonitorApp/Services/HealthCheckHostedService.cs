using HealthMonitorApp.Data;
using HealthMonitorApp.Models;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;


namespace HealthMonitorApp.Services
{
    public class HealthCheckHostedService : IHostedService, IDisposable
    {
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly ILogger<HealthCheckHostedService> _logger;
        private Timer _timer;
        private readonly IHubContext<NotificationHub> _hubContext;

        public HealthCheckHostedService(IServiceScopeFactory scopeFactory, ILogger<HealthCheckHostedService> logger,
            IHubContext<NotificationHub> hubContext)
        {
            _scopeFactory = scopeFactory;
            _logger = logger;
            _hubContext = hubContext;
        }

        public Task StartAsync(CancellationToken cancellationToken)
        {
            _logger.LogInformation("Health Check Hosted Service is starting.");

            _timer = new Timer(_ => Task.Run(DoWork), null, TimeSpan.Zero, TimeSpan.FromMinutes(30));

            return Task.CompletedTask;
        }

        private async Task DoWork()
        {
            _logger.LogInformation("Health Check Hosted Service is working.");

            List<string> failedServices = new List<string>();

            using (var scope = _scopeFactory.CreateScope())
            {
                var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
                var healthCheckService = scope.ServiceProvider.GetRequiredService<HealthCheckService>();

                List<ServiceStatus> serviceStatusList = await context.ServiceStatuses
                    .Include(apiEndPoints => apiEndPoints.ApiEndpoint)
                    .Include(apiEndPoints => apiEndPoints.ApiEndpoint.ApiGroup)
                    .ToListAsync();

                foreach (ServiceStatus serviceStatus in serviceStatusList)
                {
                    await healthCheckService.CheckServiceStatusHealthAsync(serviceStatus);

                    if (!serviceStatus.IsHealthy)
                    {
                        failedServices.Add(serviceStatus.ApiEndpoint.Name);
                    }
                }
            }

            if (failedServices.Any())
            {
                string emailBody = "The following services failed the health check: " +
                                   string.Join(", ", failedServices);
                
                var warningService = new WarningService(new Logger<WarningService>(new LoggerFactory()));
                await warningService.SendEmailAsync(
                    "msbel5@gmail.com",
                    "Health Check Failure",
                    emailBody,
                    "<strong>" + emailBody + "</strong>"  // Example of converting plain text email body to HTML
                );
                
                await warningService.SendWhatsAppMessageAsync(
                    "905555555555",
                    emailBody
                );
                
                await warningService.SendEmailViaExchangeAsync(
                    "df.muhammed.sıddık.bel@a101.com.tr",
                    "Sos Health Check Failure",
                    emailBody
                );
            }
            await _hubContext.Clients.All.SendAsync("RefreshPage");
        }


        public Task StopAsync(CancellationToken cancellationToken)
        {
            _logger.LogInformation("Health Check Hosted Service is stopping.");

            _timer?.Change(Timeout.Infinite, 0);

            return Task.CompletedTask;
        }

        public void Dispose()
        {
            _timer?.Dispose();
        }
    }
}