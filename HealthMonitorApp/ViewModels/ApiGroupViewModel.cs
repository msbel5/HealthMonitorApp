using HealthMonitorApp.Models;

namespace HealthMonitorApp.ViewModels;

public class ApiGroupViewModel
{
    ApiGroup ApiGroup { get; set; }
    
    public List<VariableViewModel> Variables { get; set; } = new List<VariableViewModel>();

}