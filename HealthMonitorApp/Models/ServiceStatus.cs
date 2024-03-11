namespace HealthMonitorApp.Models;

public class ServiceStatus
{
    public Guid Id { get; set; }
    public string Name { get; set; }
    public bool IsHealthy { get; set; }
    public DateTime CheckedAt { get; set; }
    public double ResponseTime { get; set; }
    public string? ResponseContent { get; set; }
    public string? AssertionScript { get; set; }

    // Navigation properties
    public Guid ApiEndpointId { get; set; }
    public ApiEndpoint ApiEndpoint { get; set; }
    public ICollection<ServiceStatusHistory>? ServiceStatusHistories { get; set; }
}