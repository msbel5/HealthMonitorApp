namespace HealthMonitorApp.Models;

public class ServiceStatusHistory
{
    public Guid Id { get; set; }
    public Guid ServiceStatusId { get; set; }
    public bool IsHealthy { get; set; }
    public DateTime CheckedAt { get; set; }
    public double ResponseTime { get; set; }

    public ServiceStatus ServiceStatus { get; set; }
}