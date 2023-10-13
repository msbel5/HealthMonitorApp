namespace HealthMonitorApp.Models;

public class ServiceStatus
{
    public int ID { get; set; }
    public string Name { get; set; }
    public bool IsHealthy { get; set; }
    public DateTime CheckedAt { get; set; }
    public double ResponseTime { get; set; }
    public string? ResponseContent { get; set; }

    // Navigation properties
    public int ApiEndpointID { get; set; }
    public ApiEndpoint ApiEndpoint { get; set; }
    public ICollection<ServiceStatusHistory>? ServiceStatusHistories { get; set; }
}
