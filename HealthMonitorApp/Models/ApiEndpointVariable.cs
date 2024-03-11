namespace HealthMonitorApp.Models;

public class ApiEndpointVariable
{
    public Guid ApiEndpointId { get; set; }
    public ApiEndpoint ApiEndpoint { get; set; }
    
    public Guid VariableId { get; set; }
    public Variable Variable { get; set; }

}