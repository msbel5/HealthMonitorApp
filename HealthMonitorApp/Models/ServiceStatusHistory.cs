namespace HealthMonitorApp.Models;

public class ServiceStatusHistory
{
    public int ID { get; set; }
    public int ServiceStatusID { get; set; }
    public bool IsHealthy { get; set; }
    public DateTime CheckedAt { get; set; }
    public double ResponseTime { get; set; }

    public ServiceStatus ServiceStatus { get; set; }
}

