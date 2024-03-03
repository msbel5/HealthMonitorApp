using HealthMonitorApp.Models;
using System.Text;
using System.Text.RegularExpressions;
using HealthMonitorApp.Services;

namespace HealthMonitorApp.Data
{
    public class DataSeeder(
        ApplicationDbContext context,
        ILogger<DataSeeder> logger,
        HealthCheckService healthCheckService,
        RepositoryService repositoryService)
    {
        public async Task SeedData()
        {
            if (!context.ServiceStatuses.Any())
            {
                 SeedOnDeploy();
            }
        }
        
        
        public async Task SeedDataFromRepository(RepositoryAnalysis repositoryAnalysis)
        {
            
            var repository = repositoryAnalysis;
            var repositoryName = repository.Name;
            var repositoryUrl = repository.Url;
            var repositoryBranch = repository.Branch;
            var repositoryUsername = repository.DecryptCredentials().Username;
            var repositoryPassword = repository.DecryptCredentials().Password;
            var repositoryBaseUrl = repository.BaseUrl;
            var repositoryPath = repository.Path;

            if (string.IsNullOrWhiteSpace(repositoryName) || string.IsNullOrWhiteSpace(repositoryUrl) ||
                string.IsNullOrWhiteSpace(repositoryBranch))
            {
                logger.LogError("Repository name, URL, and branch are required to seed data");
                return;
            }
            
            var repositoryData = await repositoryService.GetRepositoryAnalysisByUrlAsync(repository.Url);

            if (repositoryData == null)
            {
                logger.LogError("Failed to retrieve repository data");
                return;
            }
            
            var apiMapping = await repositoryService.GetApiMappingFromRepositoryAsync(repositoryData);

            var endpointJson = await repositoryService.ExtractControllersAndEndpointsAsJsonAsync(repositoryData);
        }



        private async void SeedOnDeploy()
        {
            if (context.ServiceStatuses.Any()) return;
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
                    ApiGroupID = googleGroup.ID
                }
            };

            context.ServiceStatuses.AddRange(googleStatus);
            await context.SaveChangesAsync();

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

            if (commandBuilder.Length > 0)
            {
                curlCommands.Add(commandBuilder.ToString().Trim());
            }

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

                    if (apiGroup.ID == 0)
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
                        ApiGroupID = apiGroup.ID,
                        ServiceStatusID = serviceStatus.ID
                    };

                    context.ApiEndpoints.Add(apiEndpoint);
                    await context.SaveChangesAsync();
                    ServiceStatus apiServiceStatus = apiEndpoint.ServiceStatus;
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
}