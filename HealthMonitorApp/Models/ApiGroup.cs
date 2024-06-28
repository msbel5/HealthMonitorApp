namespace HealthMonitorApp.Models;

public class ApiGroup
{
    public Guid Id { get; set; }
    public string Name { get; set; }

    public Guid?
        RepositoryAnalysisId
    {
        get;
        set;
    } // Foreign key property (nullable if an ApiGroup might not belong to a RepositoryAnalysis)

    public RepositoryAnalysis? RepositoryAnalysis { get; set; } // Navigation property

    public bool? IsAuthorized { get; set; }

    public ICollection<ApiEndpoint> ApiEndpoints { get; set; } = new List<ApiEndpoint>();

    public ICollection<Variable>? Variables { get; set; } = new List<Variable>();
    public string? Annotations { get; set; }
}