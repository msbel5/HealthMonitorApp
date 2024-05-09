namespace HealthMonitorApp.Models;

public class ApiEndpoint
{
    public Guid Id { get; set; }
    public string Name { get; set; }
    public string cURL { get; set; }
    public int ExpectedStatusCode { get; set; }

    // Make this nullable to allow for optional ApiGroup
    public Guid? ApiGroupId { get; set; }

    // Navigation properties
    public ApiGroup ApiGroup { get; set; }
    public Guid ServiceStatusId { get; set; }
    public ServiceStatus ServiceStatus { get; set; }

    public bool? IsAuthorized { get; set; }

    public bool? IsOpen { get; set; }
    public string? Annotations { get; set; }
    
}