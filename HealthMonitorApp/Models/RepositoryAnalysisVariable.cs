namespace HealthMonitorApp.Models;

public class RepositoryAnalysisVariable
{
    public Guid RepositoryAnalysisId { get; set; }
    public RepositoryAnalysis RepositoryAnalysis { get; set; }
    
    public Guid VariableId { get; set; }
    public Variable Variable { get; set; }
}