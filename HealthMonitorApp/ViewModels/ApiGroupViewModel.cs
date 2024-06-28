using HealthMonitorApp.Models;

namespace HealthMonitorApp.ViewModels;

public class ApiGroupViewModel
{
    public ApiGroup ApiGroup { get; set; }

    public List<Variable> Variables { get; set; } = new();
}