using System.Diagnostics;
using System.Text;
using HealthMonitorApp.Interfaces;
using HealthMonitorApp.Models;

namespace HealthMonitorApp.Tools.Providers;

public class GitVcsProvider : IVcsProvider
{
    public async Task<bool> IsRepositoryUpdatedAsync(RepositoryAnalysis? repositoryAnalysis)
    {
        // Build the URL with credentials if provided.
        var repoUrl = repositoryAnalysis.Url;
        if (!string.IsNullOrEmpty(repositoryAnalysis.EncryptedUsername) &&
            !string.IsNullOrEmpty(repositoryAnalysis.EncryptedPassword))
        {
            var uriBuilder = new UriBuilder(repositoryAnalysis.Url)
            {
                UserName = repositoryAnalysis.DecryptCredentials().Username,
                Password = repositoryAnalysis.DecryptCredentials().Password
            };
            repoUrl = uriBuilder.ToString();
        }

        var process = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = "git",
                Arguments = $"ls-remote {repoUrl} refs/heads/{repositoryAnalysis.Branch}",
                UseShellExecute = false,
                RedirectStandardOutput = true,
                CreateNoWindow = true
            }
        };

        process.Start();
        var output = await process.StandardOutput.ReadToEndAsync();
        await process.WaitForExitAsync();

        var remoteLatestCommitHash = output.Split('\t')[0]; // Extract the commit hash
        var storedLatestCommitHash = repositoryAnalysis.LatestCommitHash;


        return remoteLatestCommitHash == storedLatestCommitHash;
    }


    public async Task DownloadRepositoryAsync(RepositoryAnalysis? repositoryAnalysis)
    {
        var username = string.Empty;
        var password = string.Empty;
        if (!string.IsNullOrEmpty(repositoryAnalysis.EncryptedUsername) &&
            !string.IsNullOrEmpty(repositoryAnalysis.EncryptedPassword))
        {
            username = repositoryAnalysis.DecryptCredentials().Username;
            password = repositoryAnalysis.DecryptCredentials().Password;
        }

        var gitCloneCommand = !string.IsNullOrEmpty(repositoryAnalysis.EncryptedUsername) &&
                              !string.IsNullOrEmpty(repositoryAnalysis.EncryptedPassword)
            ? $"clone https://{username}:{password}@{repositoryAnalysis.Url.Substring(repositoryAnalysis.Url.IndexOf("://") + 3)} {repositoryAnalysis.Path}"
            : $"clone {repositoryAnalysis.Url} {repositoryAnalysis.Path}";

        var process = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = "git",
                Arguments = gitCloneCommand,
                RedirectStandardOutput = true,
                UseShellExecute = false,
                CreateNoWindow = true
            }
        };
        process.Start();
        await process.WaitForExitAsync();
        await CheckCommitHashAsync(repositoryAnalysis);
    }

    public async Task DeleteRepositoryAsync(RepositoryAnalysis? repositoryAnalysis)
    {
        if (repositoryAnalysis == null || !Directory.Exists(repositoryAnalysis.Path)) return;

        await RemoveAttributesAndDeleteAsync(new DirectoryInfo(repositoryAnalysis.Path));
    }


    public async Task UpdateRepositoryAsync(RepositoryAnalysis? repositoryAnalysis)
    {
        if (repositoryAnalysis == null)
            throw new ArgumentNullException(nameof(repositoryAnalysis));

        // The repository is not up to date, delete and re-clone it
        await DeleteRepositoryAsync(repositoryAnalysis);
        await DownloadRepositoryAsync(repositoryAnalysis);
    }


    public async Task CheckCommitHashAsync(RepositoryAnalysis repositoryAnalysis)
    {
        var process = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = "git",
                Arguments = "rev-parse HEAD",
                WorkingDirectory = repositoryAnalysis.Path,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                CreateNoWindow = true
            }
        };

        process.Start();
        var output = await process.StandardOutput.ReadToEndAsync();
        await process.WaitForExitAsync();

        repositoryAnalysis.LatestCommitHash = output.Trim();
    }


    private async Task RemoveAttributesAndDeleteAsync(DirectoryInfo directoryInfo)
    {
        foreach (var directory in
                 directoryInfo.GetDirectories())
            await RemoveAttributesAndDeleteAsync(directory); // Recursively remove attributes and delete subdirectories

        foreach (var file in directoryInfo.GetFiles())
            try
            {
                file.Attributes = FileAttributes.Normal; // Remove read-only attribute
                file.Delete();
            }
            catch (Exception ex)
            {
            }

        try
        {
            directoryInfo.Attributes = FileAttributes.Normal; // Ensure directory itself is not read-only
            directoryInfo.Delete(true); // Now safe to delete directory
        }
        catch (Exception ex)
        {
        }
    }

    public Task<bool> IsRepositoryClonedAsync(RepositoryAnalysis? repositoryAnalysis)
    {
        // Check if the directory and .git directory exist
        var isCloned = Directory.Exists(repositoryAnalysis.Path) &&
                       Directory.Exists(Path.Combine(repositoryAnalysis.Path, ".git"));
        return Task.FromResult(isCloned);
    }
}