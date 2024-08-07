namespace HealthMonitorApp.ViewModels;

public class ServiceStatusIndexViewModel
{
    public Guid ID { get; set; }
    public string Name { get; set; }
    public string ApiGroupName { get; set; }
    public string RepositoryName { get; set; }
    public double CurrentResponseTime { get; set; }
    public List<double> LastThreeResponseTimes { get; set; } = new();
    public double AverageResponseTime { get; set; }
    public bool IsHealthy { get; set; }

    public DateTime LastCheck { get; set; }
}