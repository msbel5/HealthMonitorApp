using System.Text;
using System.Text.RegularExpressions;
using HealthMonitorApp.Models;
using HealthMonitorApp.Services;
using HealthMonitorApp.Tools;
using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json;

namespace HealthMonitorApp.Data;

public class DataSeeder(
    ApplicationDbContext context,
    ILogger<DataSeeder> logger,
    HealthCheckService healthCheckService,
    RepositoryService repositoryService)
{
    public Task SeedData()
    {
        if (!context.ServiceStatuses.Any()) SeedOnDeploy();
        return  Task.CompletedTask;
    }


    public async Task SeedDataFromRepository(RepositoryAnalysis repositoryAnalysis)
    {
        var repositoryData = await repositoryService.GetRepositoryAnalysisByUrlAsync(repositoryAnalysis.Url);
        if (repositoryData == null)
        {
            logger.LogError("Failed to retrieve repository data");
            return;
        }

        var apiGroupsJson = await repositoryService.ExtractControllersAndEndpointsAsJsonAsync(repositoryData);
        var apiGroups = JsonConvert.DeserializeObject<List<ApiGroup>>(apiGroupsJson);

        if (apiGroups == null) return;

        var curlConstructor = new CurlConstructor(repositoryService, context);

        foreach (var apiGroupExt in apiGroups)
        {
            var isAuthorized = apiGroupExt.IsAuthorized != null && apiGroupExt.IsAuthorized.Value;
            var apiGroup = new ApiGroup
            {
                Name = apiGroupExt.Name.Replace("Controller", ""),
                RepositoryAnalysisId = repositoryData.Id,
                IsAuthorized = isAuthorized,
                Annotations = apiGroupExt.Annotations
            };
            context.ApiGroups.Add(apiGroup);
            await context.SaveChangesAsync();

            foreach (var apiEndpointExt in apiGroupExt.ApiEndpoints)
            {
                var annotationList =
                    apiEndpointExt.Annotations?.Split(new[] { ',', ' ' }, StringSplitOptions.RemoveEmptyEntries);
                var httpMethodAnnotation = annotationList.FirstOrDefault(a => a.StartsWith("Http"));
                var httpMethod = string.Empty;
                if (httpMethodAnnotation != null)
                    // Extract just the HTTP method part (e.g., "HttpPost" becomes "POST")
                    httpMethod = httpMethodAnnotation.Replace("Http", "");

                var serviceStatus = new ServiceStatus { Name = string.Concat(apiEndpointExt.Name, " ", httpMethod) };
                context.ServiceStatuses.Add(serviceStatus);
                await context.SaveChangesAsync();

                var isAuthorizedApiEndpoint = apiEndpointExt.IsAuthorized != null && apiEndpointExt.IsAuthorized.Value;
                var isOpenApiEndpoint = apiEndpointExt.IsOpen != null && apiEndpointExt.IsOpen.Value;
                var apiEndpoint = new ApiEndpoint
                {
                    Name = string.Concat(apiEndpointExt.Name, " ", httpMethod),
                    cURL = await curlConstructor.ConstructCurlCommand(apiGroupExt, apiEndpointExt, repositoryAnalysis),
                    ExpectedStatusCode = 200, // As mentioned, it's always 200
                    ApiGroupId = apiGroup.Id,
                    ServiceStatusId = serviceStatus.Id,
                    Annotations = apiEndpointExt.Annotations,
                    IsAuthorized = isAuthorizedApiEndpoint,
                    IsOpen = isOpenApiEndpoint,
                    Parameters = apiEndpointExt.Parameters
                };

                context.ApiEndpoints.Add(apiEndpoint);
                await context.SaveChangesAsync();

                await healthCheckService.CheckServiceStatusHealthAsync(serviceStatus);
            }
        }
    }


    private async void SeedOnDeploy()
    {
        if (context.ServiceStatuses.Any()) return;
        
        await SeedSettingsOnDeploy();
        
        await SeedDataFromCurlCommands();
        // 1. Create and add ApiGroups
        var googleGroup = new ApiGroup { Name = "Search Engines" };
        context.ApiGroups.AddRange(googleGroup);


        // Save changes to ensure ApiGroups are added first
        await context.SaveChangesAsync();

        // 3. Create and add ServiceStatuses with associated ApiEndpoints
        var googleStatus = new ServiceStatus
        {
            Name = "Google",
            IsHealthy = true,
            CheckedAt = DateTime.Now,
            ApiEndpoint = new ApiEndpoint
            {
                Name = "Google",
                cURL = "https://www.google.com",
                ExpectedStatusCode = 200,
                ApiGroupId = googleGroup.Id
            }
        };


        context.ServiceStatuses.AddRange(googleStatus);
        await context.SaveChangesAsync();
        
    }
    
    private async Task SeedSettingsOnDeploy()
    {
        if (!context.Settings.Any())
        {
                                    
            var settings = new Settings
            {
                TestInterval = TimeSpan.FromMinutes(30), // Default to 10 minutes
                NotificationEmails = "muhammet.bel@testinium.com", // Default to empty string
                SmtpServer = "smtp-mail.outlook.com", // Default to a placeholder SMTP server
                SmtpPort = 587, // Default to a common SMTP port
                SmtpUsername = "muhammet.bel@testinium.com", // Default to empty string
                SmtpPassword = "" // Default to empty string
            };
        
            context.Settings.Add(settings);
            await context.SaveChangesAsync();
        }
    }

    private async Task SeedDataFromCurlCommands()
    {
        var filePath = "Data/1.txt";

        if (!File.Exists(filePath))
        {
            logger.LogError($"The file {filePath} does not exist.");
            return;
        }

        var lines = await File.ReadAllLinesAsync(filePath);
        var curlCommands = new List<string>();
        var commandBuilder = new StringBuilder();

        foreach (var line in lines)
        {
            if (string.IsNullOrWhiteSpace(line))
            {
                if (commandBuilder.Length > 0)
                {
                    curlCommands.Add(commandBuilder.ToString().Trim());
                    commandBuilder.Clear();
                }

                continue;
            }

            commandBuilder.AppendLine(line.Trim());
        }

        if (commandBuilder.Length > 0) curlCommands.Add(commandBuilder.ToString().Trim());

        foreach (var curlCommand in curlCommands)
        {
            logger.LogInformation($"Processing cURL command: {curlCommand}");

            await using var transaction = await context.Database.BeginTransactionAsync();
            try
            {
                var match = Regex.Match(curlCommand, @"https?://[^/]+/(.*?)(?:\s|'|""|$)");

                if (!match.Success || match.Groups.Count <= 1)
                {
                    logger.LogWarning("Failed to extract URL from cURL command");
                    continue;
                }

                var urlPath = match.Groups[1].Value;
                var pathSegments = urlPath.Split('/', StringSplitOptions.RemoveEmptyEntries);

                if (pathSegments.Length < 2)
                {
                    logger.LogWarning("Failed to extract API group name and API name from URL");
                    continue;
                }

                string apiGroupName;
                string apiName;

                if (pathSegments[0].Equals("controller", StringComparison.OrdinalIgnoreCase) ||
                    pathSegments[0].Equals("api", StringComparison.OrdinalIgnoreCase))
                {
                    apiGroupName = pathSegments.Length > 1 ? pathSegments[1] : "UnknownGroup";
                    apiName = pathSegments.Length > 2 ? pathSegments[2] : "UnknownApi";
                }
                else
                {
                    apiGroupName = pathSegments[0];
                    apiName = pathSegments[1];
                }

                var apiGroup = context.ApiGroups.FirstOrDefault(ag => ag.Name == apiGroupName) ??
                               new ApiGroup { Name = apiGroupName };

                if (apiGroup.Id == Guid.Empty)
                {
                    context.ApiGroups.Add(apiGroup);
                    await context.SaveChangesAsync();
                }

                var serviceStatus = new ServiceStatus
                {
                    Name = $"{apiGroupName} - {apiName}",
                    IsHealthy = true,
                    CheckedAt = DateTime.UtcNow,
                    ResponseTime = 0
                };

                context.ServiceStatuses.Add(serviceStatus);
                await context.SaveChangesAsync();

                var apiEndpoint = new ApiEndpoint
                {
                    Name = apiName,
                    cURL = curlCommand,
                    ExpectedStatusCode = 200,
                    ApiGroupId = apiGroup.Id,
                    ServiceStatusId = serviceStatus.Id
                };

                context.ApiEndpoints.Add(apiEndpoint);
                await context.SaveChangesAsync();
                var apiServiceStatus = apiEndpoint.ServiceStatus;
                await healthCheckService.CheckServiceStatusHealthAsync(apiServiceStatus);
                await transaction.CommitAsync();

                logger.LogInformation($"Successfully processed cURL command: {curlCommand}");
            }
            catch (Exception ex)
            {
                logger.LogError($"Error processing cURL command: {ex.Message}");
                await transaction.RollbackAsync();
            }
        }

        logger.LogInformation("Completed processing all cURL commands");
    }
}