using System.Text;
using HealthMonitorApp.Data;
using HealthMonitorApp.Models;
using HealthMonitorApp.Services;
using Microsoft.EntityFrameworkCore;

namespace HealthMonitorApp.Tools;

public class CurlConstructor(RepositoryService repositoryService, ApplicationDbContext context)
{

public async Task<string> ConstructCurlCommand(ApiGroup apiGroup, ApiEndpoint apiEndpoint,
        RepositoryAnalysis repositoryAnalysis)
    {
        // Obtain the API prefix asynchronously and ensure it's correctly formatted
        var apiPrefix = await repositoryService.GetCombinedApiPrefixAsync(repositoryAnalysis);
        apiPrefix = string.IsNullOrEmpty(apiPrefix) ? "" : $"{apiPrefix.Trim('/')}/";

        // Construct the base URL
        var baseUrl = repositoryAnalysis.BaseUrl?.TrimEnd('/') ?? "localhost";

        // Sanitize apiGroupName to remove "Controller" suffix and adjust apiEndpoint.Name as necessary
        var sanitizedApiGroupName = apiGroup.Name.Replace("Controller", "");

        // Construct the full URL incorporating the base URL, API prefix, controller name, and endpoint name
        var fullUrl = $"{baseUrl}/{apiPrefix}{sanitizedApiGroupName}/{apiEndpoint.Name}".TrimEnd('/');

        // Extract the HTTP method from annotations, default to GET if not specified or found
        var httpMethod = ExtractHttpMethod(apiEndpoint.Annotations);

        // Initialize the commandBuilder with the cURL command
        var commandBuilder = new StringBuilder($"curl -X {httpMethod} \"{fullUrl}\"");

        // Append headers for JSON content type if expected
        if (apiEndpoint.Annotations?.Contains("expectsJson") == true)
            commandBuilder.Append(" -H \"Content-Type: application/json\"");

        var variables = await context.Variables
            .Where(rav => rav.RepositoryAnalysisId == repositoryAnalysis.Id)
            .ToListAsync();


        // Include authorization token if available
        foreach (var rav in variables)
        {
            // Assuming context.Variables is properly set up to include Variables in your context
            var variable = await context.Variables.FindAsync(rav.Id);
            if (variable != null)
            {
                var decryptedValue =
                    variable.DecryptVariable(); // Assuming this method exists and returns the decrypted value
                // Append each variable as a header. Assuming variable.Name holds the header name.
                commandBuilder.Append($" -H \"{variable.Name}: {decryptedValue}\"");
            }
        }

        return commandBuilder.ToString();
    }


    private string ExtractHttpMethod(string annotations)
    {
        if (string.IsNullOrEmpty(annotations)) return "GET";

        var httpMethodAnnotation = annotations.Split(new[] { ',', ' ' }, StringSplitOptions.RemoveEmptyEntries)
            .FirstOrDefault(a => a.StartsWith("Http"));

        if (httpMethodAnnotation != null) return httpMethodAnnotation.Replace("Http", "").ToUpper();

        return "GET"; // Default to GET if no specific HTTP method annotation is found
    }

    
    
}