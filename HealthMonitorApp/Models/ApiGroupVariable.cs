namespace HealthMonitorApp.Models;

public class ApiGroupVariable
{
    public Guid ApiGroupId { get; set; }
    public ApiGroup ApiGroup { get; set; }
    
    public Guid VariableId { get; set; }
    public Variable Variable { get; set; }
}