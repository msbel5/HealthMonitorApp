using HealthMonitorApp.Models;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using HealthMonitorApp.Services;

namespace HealthMonitorApp.Data
{
    public class DataSeeder
    {
        private readonly ApplicationDbContext _context;
        private readonly ILogger<DataSeeder> _logger;
        private readonly HealthCheckService _healthCheckService;

        public DataSeeder(ApplicationDbContext context, ILogger<DataSeeder> logger, HealthCheckService healthCheckService)
        {
            _context = context;
            _logger = logger;
            _healthCheckService = healthCheckService;
        }

        public async Task SeedData()
        {
            if (!_context.ServiceStatuses.Any())
            {
                await SeedDataFromCurlCommands();
            }
        }



        private void InitializeDataFromKarateFile()
        {
            if (_context.ServiceStatuses.Any()) return;
            SeedDataFromCurlCommands();
            // 1. Create and add ApiGroups
            var googleGroup = new ApiGroup { Name = "Search Engines" };
            _context.ApiGroups.AddRange(googleGroup);


            // Save changes to ensure ApiGroups are added first
            _context.SaveChanges();

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

            _context.ServiceStatuses.AddRange(googleStatus);
            _context.SaveChanges();

        }


        private async Task SeedDataFromCurlCommands()
        {
            var filePath = "Data/1.txt";

            if (!File.Exists(filePath))
            {
                _logger.LogError($"The file {filePath} does not exist.");
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
                _logger.LogInformation($"Processing cURL command: {curlCommand}");

                using (var transaction = await _context.Database.BeginTransactionAsync())
                {
                    try
                    {
                        var match = Regex.Match(curlCommand, @"https?://[^/]+/(.*?)(?:\s|'|""|$)");

                        if (!match.Success || match.Groups.Count <= 1)
                        {
                            _logger.LogWarning("Failed to extract URL from cURL command");
                            continue;
                        }

                        var urlPath = match.Groups[1].Value;
                        var pathSegments = urlPath.Split('/', StringSplitOptions.RemoveEmptyEntries);

                        if (pathSegments.Length < 2)
                        {
                            _logger.LogWarning("Failed to extract API group name and API name from URL");
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

                        var apiGroup = _context.ApiGroups.FirstOrDefault(ag => ag.Name == apiGroupName) ?? new ApiGroup { Name = apiGroupName };

                        if (apiGroup.ID == 0)
                        {
                            _context.ApiGroups.Add(apiGroup);
                            await _context.SaveChangesAsync();
                        }

                        var serviceStatus = new ServiceStatus
                        {
                            Name = $"{apiGroupName} - {apiName}",
                            IsHealthy = true,
                            CheckedAt = DateTime.UtcNow,
                            ResponseTime = 0
                        };

                        _context.ServiceStatuses.Add(serviceStatus);
                        await _context.SaveChangesAsync();

                        var apiEndpoint = new ApiEndpoint
                        {
                            Name = apiName,
                            cURL = curlCommand,
                            ExpectedStatusCode = 200,
                            ApiGroupID = apiGroup.ID,
                            ServiceStatusID = serviceStatus.ID
                        };

                        _context.ApiEndpoints.Add(apiEndpoint);
                        await _context.SaveChangesAsync();
                        ServiceStatus apiServiceStatus = apiEndpoint.ServiceStatus;
                        await _healthCheckService.CheckServiceStatusHealthAsync(apiServiceStatus);
                        await transaction.CommitAsync();

                        _logger.LogInformation($"Successfully processed cURL command: {curlCommand}");
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError($"Error processing cURL command: {ex.Message}");
                        await transaction.RollbackAsync();
                    }
                }
            }

            _logger.LogInformation("Completed processing all cURL commands");
        }




    }


}