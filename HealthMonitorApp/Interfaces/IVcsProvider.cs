using HealthMonitorApp.Models;

namespace HealthMonitorApp.Interfaces;

public interface IVcsProvider
{
    Task<bool> IsRepositoryUpdatedAsync(RepositoryAnalysis? repositoryAnalysis);
    Task DownloadRepositoryAsync(RepositoryAnalysis? repositoryAnalysis);
    Task DeleteRepositoryAsync(RepositoryAnalysis? repositoryAnalysis);
    Task UpdateRepositoryAsync(RepositoryAnalysis? repositoryAnalysis);

}