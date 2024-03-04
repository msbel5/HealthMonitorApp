using HealthMonitorApp.Models;
using HealthMonitorApp.Tools.Providers;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System.Runtime.InteropServices;
using HtmlAgilityPack;
using Newtonsoft.Json;


namespace HealthMonitorApp.Services;

using System.Diagnostics;
using System.Threading.Tasks;

public class ApplicationInspectorService
{ 

    private const string RepoPath = "Repos";
    private RepositoryService _repositoryService;

    public ApplicationInspectorService(RepositoryService repositoryService)
    {
        _repositoryService = repositoryService;
    }


    public async Task EnsureInstalledAsync()
    {
        if (!await IsInstalledAsync())
        {
            await InstallAsync();
        }
    }

    private async Task<bool> IsInstalledAsync()
    {
        var process = new Process()
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = "dotnet",
                Arguments = "tool list --global",
                RedirectStandardOutput = true,
                UseShellExecute = false,
                CreateNoWindow = true
            }
        };

        process.Start();
        string output = await process.StandardOutput.ReadToEndAsync();
        await process.WaitForExitAsync();

        return output.Contains("Microsoft.CST.ApplicationInspector.CLI");
    }

    private async Task InstallAsync()
    {
        var process = new Process()
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = "dotnet",
                Arguments = "tool install --global Microsoft.CST.ApplicationInspector.CLI",
                RedirectStandardOutput = true,
                UseShellExecute = false,
                CreateNoWindow = true
            }
        };

        process.Start();
        await process.WaitForExitAsync();
    }
    
    private async Task<string> AnalyzeAsync(RepositoryAnalysis? repositoryAnalysis)
    {
        string applicationInspector = RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? "appinspector.exe" : "appinspector";

        
        var parentDirectory = Directory.GetParent(Environment.CurrentDirectory)?.Parent?.FullName;

        // Construct the path for repositories to be beside the project directory
        var repositoryDownloadPath = Path.Combine(parentDirectory, RepoPath, repositoryAnalysis.Name);


        var arguments = $"analyze -s \"{repositoryAnalysis.Path}\" -f json"; // Use the absolute path
        var process = new Process()
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = applicationInspector,
                Arguments = arguments,
                RedirectStandardOutput = true,
                UseShellExecute = false,
                CreateNoWindow = true
            }
        };

        try
        {
            process.Start();
            var result = await process.StandardOutput.ReadToEndAsync();
            await process.WaitForExitAsync();
            return result; // JSON result of the analysis
        }
        catch (Exception ex)
        {
            // Log the error or handle it as per your application's error handling policy
            return $"Error executing Application Inspector: {ex.Message}";
        }
    }
    
    // analyze and Construct output.html report

    
    public async Task<RepositoryAnalysis?> AnalyzeRepositoryAsync(RepositoryAnalysis? repositoryAnalysis)
    {
        var codeCheckService = new VcsService(new GitVcsProvider());
        // Example: Ensure the repository is up-to-date or clone it
        await codeCheckService.EnsureRepositoryIsReady(repositoryAnalysis);

        // Perform the analysis
        var analysisResult = await AnalyzeAsync(repositoryAnalysis);

        // Parse the result and update the RepositoryAnalysis model
        var updatedAnalysis = ParseAnalysisResult(analysisResult, repositoryAnalysis);

        return updatedAnalysis;
    }
    
    private RepositoryAnalysis? ParseAnalysisResult(string analysisResult, RepositoryAnalysis? repositoryAnalysis)
    {
        // Find the index of the first opening brace `{` which marks the start of the JSON structure
        int jsonStartIndex = analysisResult.IndexOf('{');

        if (jsonStartIndex != -1)
        {
            // Extract the JSON part from the analysisResult starting from the found index
            string jsonPart = analysisResult.Substring(jsonStartIndex);

        }
        else
        {
            // If no JSON structure is found, you might want to log an error or assign a default value
            repositoryAnalysis.BaseUrl = "{no valid json}"; // Assigning an empty JSON object as a default
        }

        return repositoryAnalysis;
    }
    
    public async Task<RepositoryAnalysis?> AnalyzeRepositoryForEndpointsAsync(RepositoryAnalysis? repositoryAnalysis)
    {
        // First, generate the JSON summary of controllers and endpoints
        string jsonSummary = await _repositoryService.ExtractControllersAndEndpointsAsJsonAsync(repositoryAnalysis);

        // Deserialize the JSON summary back into a list of ControllerInfo objects
        var controllersInfo = JsonConvert.DeserializeObject<List<ApiGroup>>(jsonSummary);

        // Use the deserialized list to populate the repositoryAnalysis object
        int totalControllers = controllersInfo.Count;
        int totalEndpoints = controllersInfo.Sum(c => c.ApiEndpoints.Count);
        int totalPublicEndpoints = controllersInfo.Sum(c => c.ApiEndpoints.Count(e => e.IsOpen != null && e.IsOpen.Value));

        // Optional: Populate other analysis details as needed
        repositoryAnalysis.NumberOfControllers = totalControllers;
        repositoryAnalysis.NumberOfEndpoints = totalEndpoints;
        repositoryAnalysis.NumberOfPublicEndpoints = totalPublicEndpoints;

        // Further processing if needed

        return repositoryAnalysis;
    }
    
    
    
    public async Task GenerateReportAsync(RepositoryAnalysis? repositoryAnalysis)
    {
        
        string applicationInspector = RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? "appinspector.exe" : "appinspector";


        var arguments = $"analyze -s \"{repositoryAnalysis.Path}\" -o \"{repositoryAnalysis.GetReportPath()}\""; // Use the absolute path
        var process = new Process()
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = applicationInspector,
                Arguments = arguments,
                RedirectStandardOutput = true,
                UseShellExecute = false,
                CreateNoWindow = true
            }
        };

        try
        {
            process.Start();
            var result = await process.StandardOutput.ReadToEndAsync();
            await process.WaitForExitAsync();
        }
        catch (Exception ex)
        {
            // Log the error or handle it as per your application's error handling policy
            throw new Exception($"Error executing Application Inspector: {ex.Message}");
        }
    }

    
    }