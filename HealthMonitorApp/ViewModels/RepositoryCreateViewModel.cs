using HealthMonitorApp.Models;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace HealthMonitorApp.ViewModels;

public class RepositoryCreateViewModel
{
    public string Name { get; set; }
    public string Url { get; set; }
    public string Branch { get; set; }
    public string? Username { get; set; }
    public string? Password { get; set; }
    public string? BaseUrl { get; set; }

    public bool IntegrateEndpoints { get; set; } = false;

    public List<Variable>? Variables { get; set; }

    public string? ExcludedControllers { get; set; }
    public string? ExcludedMethods { get; set; }

    public List<Guid>? SelectedApiGroupIds { get; set; } = new();
    public List<SelectListItem> ApiGroups { get; set; } = new();
}