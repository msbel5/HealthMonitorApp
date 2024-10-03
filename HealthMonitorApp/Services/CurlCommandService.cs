using System.Text;
using System.Text.RegularExpressions;
using HealthMonitorApp.Data;
using HealthMonitorApp.Dtos;
using HealthMonitorApp.Interfaces;
using HealthMonitorApp.Models;

namespace HealthMonitorApp.Services;

public class CurlCommandService : ICurlCommandService
{
    private readonly ApplicationDbContext _context;
    private readonly ILogger<CurlCommandService> _logger;
    private readonly HealthCheckService _healthCheckService;

    public CurlCommandService(ApplicationDbContext context, ILogger<CurlCommandService> logger, HealthCheckService healthCheckService)
    {
        _context = context;
        _logger = logger;
        _healthCheckService = healthCheckService;
    }

    public async Task SaveCurlCommandDto(CurlCommandDto curlCommandDto)
    {
        _logger.LogInformation($"Saving cURL command: {curlCommandDto.Content}");
        var (variables, curlCommand) = ParseCurlContent(curlCommandDto.Content);

        if (string.IsNullOrWhiteSpace(curlCommand))
        {
            throw new ArgumentException("No valid cURL command found.");
        }

        // Extract the URL
        var url = ExtractUrlFromCurlCommand(curlCommand);
        var apiGroupName = url.Item1;
        var apiName = url.Item2;

        // Find or create the ApiGroup
        var apiGroup = await GetOrCreateApiGroup(apiGroupName);

        // Add the variables to the database
        await SaveVariables(variables, apiGroup.Id);

        // Create the ServiceStatus
        var serviceStatus = new ServiceStatus
        {
            Name = $"{apiGroupName} - {apiName}",
            IsHealthy = true,
            CheckedAt = DateTime.UtcNow,
            ResponseTime = 0
        };

        _context.ServiceStatuses.Add(serviceStatus);
        await _context.SaveChangesAsync();

        // Save the cURL command in ApiEndpoint
        var apiEndpoint = new ApiEndpoint
        {
            Name = apiName,
            cURL = curlCommand,
            ExpectedStatusCode = 200,
            ApiGroupId = apiGroup.Id,
            ServiceStatusId = serviceStatus.Id
        };

        _context.ApiEndpoints.Add(apiEndpoint);
        await _context.SaveChangesAsync();

        // Perform health check
        await _healthCheckService.CheckServiceStatusHealthAsync(apiEndpoint.ServiceStatus);
    }

    public async Task SaveAllCurlCommandDtos(List<CurlCommandDto> curlCommandDtos)
    {
        foreach (var curlCommandDto in curlCommandDtos)
        {
            await SaveCurlCommandDto(curlCommandDto);
        }
    }

    private async Task<ApiGroup> GetOrCreateApiGroup(string apiGroupName)
    {
        var apiGroup = _context.ApiGroups.FirstOrDefault(ag => ag.Name == apiGroupName);
        if (apiGroup == null)
        {
            apiGroup = new ApiGroup { Name = apiGroupName };
            _context.ApiGroups.Add(apiGroup);
            await _context.SaveChangesAsync();
        }
        return apiGroup;
    }

    private async Task SaveVariables(Dictionary<string, string> variables, Guid apiGroupId)
    {
        foreach (var variable in variables)
        {
            var dbVariable = new Variable
            {
                Name = variable.Key,
                Value = variable.Value,
                ApiGroupId = apiGroupId
            };
            _context.Variables.Add(dbVariable);
        }
        await _context.SaveChangesAsync();
    }

    private (string, string) ExtractUrlFromCurlCommand(string curlCommand)
    {
        var match = Regex.Match(curlCommand, @"https?://[^/]+/(.*?)(?:\s|'|""|$)");
        if (!match.Success || match.Groups.Count <= 1)
        {
            throw new ArgumentException("Failed to extract URL from cURL command.");
        }

        var urlPath = match.Groups[1].Value;
        var pathSegments = urlPath.Split('/', StringSplitOptions.RemoveEmptyEntries);

        if (pathSegments.Length < 2)
        {
            throw new ArgumentException("Failed to extract API group name and API name from URL.");
        }

        return (pathSegments[0], pathSegments[1]);
    }

    private (Dictionary<string, string> Variables, string CurlCommand) ParseCurlContent(string content)
    {
        var variables = new Dictionary<string, string>();
        var curlCommand = new StringBuilder();

        using (var reader = new StringReader(content))
        {
            string line;
            var inCurlCommand = false;

            while ((line = reader.ReadLine()) != null)
            {
                line = line.Trim();

                if (line.StartsWith("#")) continue;

                var match = Regex.Match(line, @"^(\w+)=""(.*)""$");
                if (match.Success)
                {
                    variables[match.Groups[1].Value] = match.Groups[2].Value;
                    continue;
                }

                if (line.StartsWith("curl"))
                {
                    inCurlCommand = true;
                }

                if (inCurlCommand)
                {
                    curlCommand.AppendLine(line);
                }
            }
        }

        return (variables, curlCommand.ToString());
    }
}
