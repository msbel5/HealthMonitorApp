namespace HealthMonitorApp.Models;

public class ApiGroup
{
    public int ID { get; set; }
    public string Name { get; set; }
    public ICollection<ApiEndpoint> ApiEndpoints { get; set; } = new List<ApiEndpoint>();
}

