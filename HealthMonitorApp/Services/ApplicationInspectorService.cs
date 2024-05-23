using System.Diagnostics;
using System.Runtime.InteropServices;
using HealthMonitorApp.Models;
using HealthMonitorApp.Tools.Providers;
using Newtonsoft.Json;

namespace HealthMonitorApp.Services;

using Tools;

public class ApplicationInspectorService(RepositoryService repositoryService, ReportHandler reportHandler)
{
    private const string RepoPath = "Repos";
    private readonly Logger<ApplicationInspectorService> _logger = new Logger<ApplicationInspectorService>(new LoggerFactory());
    
    public async Task<RepositoryAnalysis?> AnalyzeAsync(RepositoryAnalysis? repositoryAnalysis)
    {
        var updatedAnalysis = await AnalyzeRepositoryAsync(repositoryAnalysis);
        
        updatedAnalysis = await AnalyzeRepositoryForEndpointsAsync(updatedAnalysis);
        _logger.LogInformation("Analysis completed for {RepositoryName}", updatedAnalysis.Name);
        
        await GenerateReportAsync(updatedAnalysis);
        _logger.LogInformation("Report generated for {RepositoryName}", updatedAnalysis.Name);
        await reportHandler.ModifyAndSaveReport(updatedAnalysis);
        _logger.LogInformation("Report modified and saved for {RepositoryName}", updatedAnalysis.Name);
        return updatedAnalysis;
    }

    public async Task EnsureAppInspectorInstalledAsync()
    {
        if (!await IsAppInspectorInstalledAsync())
        {
            _logger.LogInformation("Installing Application Inspector...");
            await InstallAppInspectorAsync();
        }
    }
    
    private async Task<bool> IsAppInspectorInstalledAsync()
    {
        var process = new Process
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
        var output = await process.StandardOutput.ReadToEndAsync();
        await process.WaitForExitAsync();
        return output.Contains("Microsoft.CST.ApplicationInspector.CLI");
    }

    private async Task InstallAppInspectorAsync()
    {
        var process = new Process
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

    private async Task<string> AnalyzeWithAppInspectorAsync(RepositoryAnalysis? repositoryAnalysis)
    {
        var applicationInspector = GetApplicationInspectorPath();
        

        var arguments = $"analyze -s \"{repositoryAnalysis.Path}\" -f json"; // Use the absolute path
        var process = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = applicationInspector,
                Arguments = arguments,
                RedirectStandardOutput = true,
                RedirectStandardError = true, // Add this to capture standard error
                UseShellExecute = false,
                CreateNoWindow = true
            }
        };

        try
        {
            process.Start();
            var result = await process.StandardOutput.ReadToEndAsync();
            var errors = await process.StandardError.ReadToEndAsync(); // Capture any errors
            await process.WaitForExitAsync();
            _logger.LogInformation("Analysis completed for {RepositoryName}", repositoryAnalysis.Name);
            return result; // JSON result of the analysis
        }
        catch (Exception ex)
        {
            // Log the error or handle it as per your application's error handling policy
            _logger.LogError(ex, "Error executing Application Inspector");
            return $"Error executing Application Inspector: {ex.Message}";
        }
    }

    // analyze and Construct output.html report
    public async Task<RepositoryAnalysis?> AnalyzeRepositoryAsync(RepositoryAnalysis? repositoryAnalysis)
    {
        var codeCheckService = new VcsService(new GitVcsProvider());
        // Example: Ensure the repository is up-to-date or clone it
        await codeCheckService.EnsureRepositoryIsReady(repositoryAnalysis);
        _logger.LogInformation("Repository is ready for analysis");
        // Perform the analysis
        var analysisResult = await AnalyzeWithAppInspectorAsync(repositoryAnalysis);
        // Parse the result and update the RepositoryAnalysis model
        var updatedAnalysis = ParseAnalysisResult(analysisResult, repositoryAnalysis);

        return updatedAnalysis;
    }
    


    private RepositoryAnalysis? ParseAnalysisResult(string analysisResult, RepositoryAnalysis? repositoryAnalysis)
    {
        // Find the index of the first opening brace `{` which marks the start of the JSON structure
        var jsonStartIndex = analysisResult.IndexOf('{');

        if (jsonStartIndex != -1)
        {
            // Extract the JSON part from the analysisResult starting from the found index
            var jsonPart = analysisResult.Substring(jsonStartIndex);
        }
        else
        {
            // If no JSON structure is found, you might want to log an error or assign a default value
            repositoryAnalysis.BaseUrl = "{no valid json}"; // Assigning an empty JSON object as a default
        }
        _logger.LogInformation("Analysis result parsed for {RepositoryName}", repositoryAnalysis.Name);
        return repositoryAnalysis;
    }

    public async Task<RepositoryAnalysis?> AnalyzeRepositoryForEndpointsAsync(RepositoryAnalysis? repositoryAnalysis)
    {
        // First, generate the JSON summary of controllers and endpoints
        var jsonSummary = await repositoryService.ExtractControllersAndEndpointsAsJsonAsync(repositoryAnalysis);
        //excluded controllers and apienpoints
            
        // Deserialize the JSON summary back into a list of ControllerInfo objects
        var controllersInfo = JsonConvert.DeserializeObject<List<ApiGroup>>(jsonSummary);

        // Use the deserialized list to populate the repositoryAnalysis object
        var totalControllers = controllersInfo.Count;
        var totalEndpoints = controllersInfo.Sum(c => c.ApiEndpoints.Count);
        var totalPublicEndpoints =
            controllersInfo.Sum(c => c.ApiEndpoints.Count(e => e.IsOpen != null && e.IsOpen.Value));

        // Optional: Populate other analysis details as needed
        repositoryAnalysis.NumberOfControllers = totalControllers;
        repositoryAnalysis.NumberOfEndpoints = totalEndpoints;
        repositoryAnalysis.NumberOfPublicEndpoints = totalPublicEndpoints;

        // Further processing if needed
        _logger.LogInformation("Endpoints analyzed for {RepositoryName}", repositoryAnalysis.Name);
        return repositoryAnalysis;
    }



    public static async Task GenerateReportAsync(RepositoryAnalysis? repositoryAnalysis)
    {
        var logger = new Logger<ApplicationInspectorService>(new LoggerFactory());
        logger.LogInformation("Generating report for {RepositoryName}", repositoryAnalysis.Name);
        var applicationInspector = GetApplicationInspectorPath();
        
        logger.LogInformation("Application Inspector path: {ApplicationInspectorPath}", applicationInspector);

        var arguments =
            $"analyze -s \"{repositoryAnalysis.Path}\" -o \"{repositoryAnalysis.GetReportPath()}\""; // Use the absolute path
        
        logger.LogInformation("Arguments: {Arguments}", arguments);
        
        var process = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = applicationInspector,
                Arguments = arguments,
                RedirectStandardError = true, // Add this to capture standard error
                RedirectStandardOutput = true,
                UseShellExecute = false,
                CreateNoWindow = true
            }
        };

        logger.LogInformation("Starting Application Inspector process...");
        
        try
        {
            process.Start();
            var result = await process.StandardOutput.ReadToEndAsync();
            var errors = await process.StandardError.ReadToEndAsync(); // Capture any errors
            logger.LogInformation("Report generation continue for {RepositoryName} with {results} and {errors}", repositoryAnalysis.Name, result, errors);
            await process.WaitForExitAsync();
            logger.LogInformation("Report generated for {RepositoryName}", repositoryAnalysis.Name);
        }
        catch (Exception ex)
        {
            // Log the error or handle it as per your application's error handling policy,
            logger.LogError(ex, "Error executing Application Inspector");
            throw new Exception($"Error executing Application Inspector: {ex.Message}");
        }
    }

    private static string GetApplicationInspectorPath()
    {
        var applicationInspector =
            RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? "appinspector.exe" : "appinspector";
        return applicationInspector;
    }
    

}