using System.Diagnostics;
using HealthMonitorApp.Interfaces;
using HealthMonitorApp.Models;

namespace HealthMonitorApp.Tools.Providers;

public class GitVcsProvider : IVcsProvider
{
    public async Task<bool> IsRepositoryUpdatedAsync(RepositoryAnalysis? repositoryAnalysis)
    {
        // Build the URL with credentials if provided.
        string repoUrl = repositoryAnalysis.Url;
        if (!string.IsNullOrEmpty(repositoryAnalysis.EncryptedUsername) && !string.IsNullOrEmpty(repositoryAnalysis.EncryptedPassword))
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
                CreateNoWindow = true,
            }
        };

        process.Start();
        var output = await process.StandardOutput.ReadToEndAsync();
        await process.WaitForExitAsync();

        var remoteLatestCommitHash = output.Split('\t')[0]; // Extract the commit hash
        var storedLatestCommitHash = repositoryAnalysis.LatestCommitHash;

        return remoteLatestCommitHash == storedLatestCommitHash;
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
                CreateNoWindow = true,
            }
        };

        process.Start();
        var output = await process.StandardOutput.ReadToEndAsync();
        await process.WaitForExitAsync();

        repositoryAnalysis.LatestCommitHash = output.Trim();
    }


    public async Task DownloadRepositoryAsync(RepositoryAnalysis? repositoryAnalysis)
    {
        string username = String.Empty; 
        string password = String.Empty; 
        if (!string.IsNullOrEmpty(repositoryAnalysis.EncryptedUsername) && !string.IsNullOrEmpty(repositoryAnalysis.EncryptedPassword))
        {
            username = repositoryAnalysis.DecryptCredentials().Username;
            password = repositoryAnalysis.DecryptCredentials().Password;
        }
        
        var gitCloneCommand = !string.IsNullOrEmpty(repositoryAnalysis.EncryptedUsername) && !string.IsNullOrEmpty(repositoryAnalysis.EncryptedPassword)
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
                CreateNoWindow = true,
            }
        };
        process.Start();
        await process.WaitForExitAsync();
    }

    public async Task DeleteRepositoryAsync(RepositoryAnalysis? repositoryAnalysis)
    {
        // Checks if the directory exists before attempting to delete.
        if (Directory.Exists(repositoryAnalysis.Path))
        {
            Directory.Delete(repositoryAnalysis.Path, true); // true => delete recursively
        }
        await Task.CompletedTask;
    }
    
    public async Task UpdateRepositoryAsync(RepositoryAnalysis? repositoryAnalysis)
    {
        string username = String.Empty; 
        string password = String.Empty; 
        if (!string.IsNullOrEmpty(repositoryAnalysis.EncryptedUsername) && !string.IsNullOrEmpty(repositoryAnalysis.EncryptedPassword))
        {
            username = repositoryAnalysis.DecryptCredentials().Username;
            password = repositoryAnalysis.DecryptCredentials().Password;
        }
        // Ensure the directory exists and is a git repository
        if (Directory.Exists(repositoryAnalysis.Path) && Directory.Exists(Path.Combine(repositoryAnalysis.Path, ".git")))
        {
            // Prepare git pull command with credentials if provided
            var gitPullCommand = !string.IsNullOrEmpty(username) && !string.IsNullOrEmpty(password)
                ? $"-c http.extraheader=\"AUTHORIZATION: basic {Convert.ToBase64String(System.Text.Encoding.ASCII.GetBytes($"{username}:{password}"))}\" pull origin {repositoryAnalysis.Branch}"
                : $"pull origin {repositoryAnalysis.Branch}";

            var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = "git",
                    Arguments = gitPullCommand,
                    WorkingDirectory = repositoryAnalysis.Path,
                    RedirectStandardOutput = true,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                }
            };

            process.Start();
            await process.WaitForExitAsync();
        }
        else
        {
            throw new InvalidOperationException("The specified path does not exist or is not a git repository.");
        }
    }
    
    public Task<bool> IsRepositoryClonedAsync(RepositoryAnalysis? repositoryAnalysis)
    {
        // Check if the directory and .git directory exist
        bool isCloned = Directory.Exists(repositoryAnalysis.Path) && Directory.Exists(Path.Combine(repositoryAnalysis.Path, ".git"));
        return Task.FromResult(isCloned);
    }

    
}

