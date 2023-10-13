using HealthMonitorApp.Models;

namespace HealthMonitorApp.ViewModels;

public class ServiceStatusEditViewModel
{
    public int ID { get; set; }
    public string Name { get; set; }
    public int ExpectedStatusCode { get; set; }
    public string CURL { get; set; }
    public int? ApiGroupID { get; set; }
    public string? NewApiGroupName { get; set; }
    public List<ApiGroup>? ApiGroups { get; set; }
}
