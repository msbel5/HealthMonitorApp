namespace HealthMonitorApp.Models;

public class ApiGroup
{
    public int ID { get; set; }
    public string Name { get; set; }
    public int? RepositoryAnalysisId { get; set; } // Foreign key property (nullable if an ApiGroup might not belong to a RepositoryAnalysis)
    public RepositoryAnalysis? RepositoryAnalysis { get; set; } // Navigation property

    public bool? IsAuthorized { get; set; }

    public ICollection<ApiEndpoint> ApiEndpoints { get; set; } = new List<ApiEndpoint>();
    public string? Annotations { get; set; } 
}

