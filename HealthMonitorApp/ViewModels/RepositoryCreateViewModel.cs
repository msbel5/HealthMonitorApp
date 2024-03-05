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
    public string? Variables { get; set; }
}