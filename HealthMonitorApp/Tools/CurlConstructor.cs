using System.Text;
using System.Text.RegularExpressions;
using HealthMonitorApp.Data;
using HealthMonitorApp.Models;
using HealthMonitorApp.Services;
using Microsoft.AspNetCore.Components;
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

        // Extract route from controller annotations if available
        var controllerRoute = GetControllerRouteAnnotation(apiGroup);
        var routePrefix = string.IsNullOrEmpty(controllerRoute) ? "" : $"{controllerRoute.Trim('/')}/";
        
        // Construct the base URL
        var baseUrl = repositoryAnalysis.BaseUrl?.TrimEnd('/') ?? "localhost";

        // Sanitize apiGroupName to remove "Controller" suffix and adjust apiEndpoint.Name as necessary
        var sanitizedApiGroupName = apiGroup.Name.Replace("Controller", "");

        // Construct the full URL incorporating the base URL, API prefix, controller name, and endpoint name
        var fullUrl = $"{baseUrl}/{apiPrefix}{routePrefix}{sanitizedApiGroupName}/{apiEndpoint.Name}".TrimEnd('/');

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

    private string GetControllerRouteAnnotation(ApiGroup apiGroup)
    {
        if (apiGroup.Annotations == null)
        {
            return ""; // Return empty if no annotations are present
        }
        
        var annotations = apiGroup.Annotations;

        // Split the annotations by ',' to handle multiple annotations
        var annotationParts = annotations.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries);
    
        foreach (var part in annotationParts)
        {
            // Check for the Route attribute in each part
            var routeMatch = Regex.Match(part.Trim(), "Route\\(\"([^\"]+)\"", RegexOptions.IgnoreCase);
            if (routeMatch.Success)
            {
                var routeTemplate = routeMatch.Groups[1].Value;
                routeTemplate = routeTemplate.TrimStart('/');

                // Split the route by '/' and filter out dynamic segments like '[controller]'
                var staticSegments = routeTemplate
                    .Split('/')
                    .TakeWhile(segment => !segment.Contains('[') && !segment.Contains(']'))
                    .ToArray();

                // Join the static segments back to form the cleaned-up route prefix
                var cleanedRoute = string.Join("/", staticSegments);
                return cleanedRoute;
            }
        }

        return ""; // Return empty if no route is found or if the first segment is not needed
    }

    private string ExtractHttpMethod(string annotations)
    {
        if (string.IsNullOrEmpty(annotations)) return "GET";

        // Use regular expressions to identify and extract HTTP methods from annotations
        var httpMethodMatch = Regex.Match(annotations, "(Http(Get|Post|Put|Delete|Patch|Head|Options|Trace|Connect))", RegexOptions.IgnoreCase);
        if (httpMethodMatch.Success)
        {
            var httpMethodAnnotation = httpMethodMatch.Groups[2].Value;  // Capture only the method part (GET, POST, etc.)
            return httpMethodAnnotation.ToUpper();
        }

        return "GET"; // Default to GET if no specific HTTP method annotation is found
    }

    
    private string ExtractHttpMethodOld(string annotations)
    {
        if (string.IsNullOrEmpty(annotations)) return "GET";

        var httpMethodAnnotation = annotations.Split(new[] { ',', ' ' }, StringSplitOptions.RemoveEmptyEntries)
            .FirstOrDefault(a => a.StartsWith("Http"));

        if (httpMethodAnnotation != null) return httpMethodAnnotation.Replace("Http", "").ToUpper();

        return "GET"; // Default to GET if no specific HTTP method annotation is found
    }


    
    
}