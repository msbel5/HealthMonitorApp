namespace HealthMonitorApp.ViewModels;

public class ServiceStatusIndexViewModel
{
    public int ID { get; set; }
    public string Name { get; set; }
    public string ApiGroupName { get; set; }
    public double CurrentResponseTime { get; set; }
    public List<double> LastThreeResponseTimes { get; set; } = new List<double>();
    public double AverageResponseTime { get; set; }
    public bool IsHealthy { get; set; }

    public DateTime LastCheck { get; set; }
}