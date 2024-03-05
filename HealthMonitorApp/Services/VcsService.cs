using HealthMonitorApp.Models;
using HealthMonitorApp.Tools.Providers;

namespace HealthMonitorApp.Services;

public class VcsService
{
    private readonly GitVcsProvider _vcsProvider;


    public VcsService(GitVcsProvider vcsProvider)
    {
        _vcsProvider = vcsProvider;
    }


    public async Task<bool> IsRepositoryUpdatedAsync(RepositoryAnalysis? repositoryAnalysis)
    {
        return await _vcsProvider.IsRepositoryUpdatedAsync(repositoryAnalysis);
    }

    public async Task DownloadRepositoryAsync(RepositoryAnalysis? repositoryAnalysis)
    {
        try
        {
            if (await _vcsProvider.IsRepositoryClonedAsync(repositoryAnalysis))
            {
                if (!await IsRepositoryUpdatedAsync(repositoryAnalysis))
                    await _vcsProvider.UpdateRepositoryAsync(repositoryAnalysis);
            }
            else
            {
                await _vcsProvider.DownloadRepositoryAsync(repositoryAnalysis);
            }
        }
        catch (Exception e)
        {
            await _vcsProvider.DeleteRepositoryAsync(repositoryAnalysis);
            await _vcsProvider.DownloadRepositoryAsync(repositoryAnalysis);
        }
    }

    public async Task DeleteRepositoryAsync(RepositoryAnalysis? repositoryAnalysis)
    {
        await _vcsProvider.DeleteRepositoryAsync(repositoryAnalysis);
    }

    public async Task UpdateRepositoryAsync(RepositoryAnalysis? repositoryAnalysis)
    {
        await _vcsProvider.UpdateRepositoryAsync(repositoryAnalysis);
    }


    public async Task EnsureRepositoryIsReady(RepositoryAnalysis? repositoryAnalysis)
    {
        if (!await _vcsProvider.IsRepositoryUpdatedAsync(repositoryAnalysis))
            await _vcsProvider.UpdateRepositoryAsync(repositoryAnalysis);
    }
}