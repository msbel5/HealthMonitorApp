using HealthMonitorApp.Data;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;

namespace HealthMonitorApp.Services;

public class HealthCheckHostedService : IHostedService, IDisposable
{
    private readonly IHubContext<NotificationHub> _hubContext;
    private readonly ILogger<HealthCheckHostedService> _logger;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly WarningService _warningService;
    private Timer _timer;

    public HealthCheckHostedService(IServiceScopeFactory scopeFactory, ILogger<HealthCheckHostedService> logger,
        IHubContext<NotificationHub> hubContext, WarningService warningService)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
        _hubContext = hubContext;
        _warningService = warningService;
    }

    public void Dispose()
    {
        _timer?.Dispose();
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Health Check Hosted Service is starting.");

        _timer = new Timer(_ => Task.Run(DoWork), null, TimeSpan.Zero, TimeSpan.FromMinutes(30));

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

            var serviceStatusList = await context.ServiceStatuses
                .Include(apiEndPoints => apiEndPoints.ApiEndpoint)
                .Include(apiEndPoints => apiEndPoints.ApiEndpoint.ApiGroup)
                .ToListAsync();

            foreach (var serviceStatus in serviceStatusList)
            {
                await healthCheckService.CheckServiceStatusHealthAsync(serviceStatus);

                if (!serviceStatus.IsHealthy) failedServices.Add(serviceStatus.ApiEndpoint.Name);
            }
        }

        if (failedServices.Any())
        {
            var emailBody = "The following services failed the health check: ";
            _logger.LogWarning("The following services failed the health check: {Services}", failedServices);
            foreach (var failedService in failedServices)
            {
                emailBody += failedService + ", ";

            }

            //var warningService = new WarningService(new Logger<WarningService>(new LoggerFactory()));
            
            await _warningService.SendEmailViaExchangeAsync(
                "df.muhammed.sıddık.bel@a101.com.tr",
                "Sos Health Check Failure",
                emailBody
            );
        }

        await _hubContext.Clients.All.SendAsync("RefreshPage");
    }
}