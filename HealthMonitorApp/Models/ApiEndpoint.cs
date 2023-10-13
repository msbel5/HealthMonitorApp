namespace HealthMonitorApp.Models;

public class ApiEndpoint
{
    public int ID { get; set; }
    public string Name { get; set; }
    public string cURL { get; set; }
    public int ExpectedStatusCode { get; set; }

    // Make this nullable to allow for optional ApiGroup
    public int? ApiGroupID { get; set; }

    // Navigation properties
    public ApiGroup ApiGroup { get; set; }
    public int ServiceStatusID { get; set; }
    public ServiceStatus ServiceStatus { get; set; }
}