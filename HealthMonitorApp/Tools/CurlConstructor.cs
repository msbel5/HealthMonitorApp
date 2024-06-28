using System.Text;
using System.Text.RegularExpressions;
using HealthMonitorApp.Data;
using HealthMonitorApp.Models;
using HealthMonitorApp.Services;
using System.Web;
using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace HealthMonitorApp.Tools;

public class CurlConstructor(RepositoryService repositoryService, ApplicationDbContext context)
{
    public async Task<string> ConstructCurlCommand(ApiGroup apiGroup, ApiEndpoint apiEndpoint,
        RepositoryAnalysis repositoryAnalysis)
    {
        var apiPrefix = await repositoryService.GetCombinedApiPrefixAsync(repositoryAnalysis);
        apiPrefix = string.IsNullOrEmpty(apiPrefix) ? "" : $"{apiPrefix.Trim('/')}/";

        var controllerRoute = GetControllerRouteAnnotation(apiGroup);
        var routePrefix = string.IsNullOrEmpty(controllerRoute) ? "" : $"{controllerRoute.Trim('/')}/";

        var baseUrl = repositoryAnalysis.BaseUrl?.TrimEnd('/') ?? "localhost";
        var sanitizedApiGroupName = apiGroup.Name.Replace("Controller", "");
        var fullUrl =
            new StringBuilder(
                $"{baseUrl}/{apiPrefix}{routePrefix}{sanitizedApiGroupName}/{apiEndpoint.Name}".TrimEnd('/'));
        var httpMethod = ExtractHttpMethod(apiEndpoint.Annotations);
        var commandBuilder = new StringBuilder($"curl -X {httpMethod} ");

        var formData = new List<string>();
        var hasQueryParameters = false;
        var isJsonRequired = false;

        foreach (var parameter in JArray.Parse(apiEndpoint.Parameters))
        {
            var paramName = parameter["Name"].ToString();
            var paramSource = parameter["Source"].ToString();
            var isComplex = parameter["IsComplex"]?.ToObject<bool>() ?? false;

            if (isComplex)
            {
                var properties = parameter["Properties"] as JArray;
                FlattenComplexType(paramName, properties, formData);
            }
            else
            {
                var defaultValue = parameter["DefaultValue"].ToString();
                if (paramSource == "body") isJsonRequired = true;
                AddFormData(paramName, defaultValue, paramSource, formData, ref hasQueryParameters, fullUrl);
            }
        }

        if (formData.Count > 0)
        {
            if (isJsonRequired)
                commandBuilder.Append(
                    $"--header \"Content-Type: application/json\" --data '{string.Join("&", formData)}' ");
            else
                commandBuilder.Append(
                    $"--header \"Content-Type: application/x-www-form-urlencoded\" {string.Join(" ", formData)} ");
        }

        commandBuilder.Append($"\"{fullUrl}\"");

        var variables = await context.Variables
            .Where(rav => rav.RepositoryAnalysisId == repositoryAnalysis.Id)
            .ToListAsync();

        foreach (var rav in variables)
        {
            var variable = await context.Variables.FindAsync(rav.Id);
            if (variable != null)
            {
                var decryptedValue = variable.DecryptVariable();
                commandBuilder.Append($"--header \"{variable.Name}: {decryptedValue}\" ");
            }
        }

        return commandBuilder.ToString();
    }

    private void FlattenComplexType(string parentName, JArray properties, List<string> formData)
    {
        foreach (var property in properties)
        {
            var propertyName = property["Name"].ToString();
            var defaultValue = property["DefaultValue"].ToString();

            var isComplex = property["IsComplex"]?.ToObject<bool>() ?? false;


            var flattenedName = $"{parentName}.{propertyName}";
            var temp = false;

            // Check if the default value is a JSON string
            if (IsJsonString(defaultValue))
            {
                var jsonObject = JObject.Parse(defaultValue);
                foreach (var prop in jsonObject.Properties())
                    FlattenComplexType(flattenedName,
                        JArray.FromObject(new[]
                        {
                            new
                            {
                                Name = prop.Name, DefaultValue = prop.Value.ToString(),
                                IsComplex = IsJsonString(prop.Value.ToString())
                            }
                        }), formData);
            }
            else if (isComplex)
            {
                var nestedProperties = property["Properties"] as JArray;
                FlattenComplexType(flattenedName, nestedProperties, formData);
            }
            else
            {
                AddFormData(flattenedName, defaultValue, "body", formData, ref temp);
            }
        }
    }


    private void AddFormData(string paramName, string defaultValue, string paramSource, List<string> formData,
        ref bool hasQueryParameters, StringBuilder fullUrl = null)
    {
        var flatParamName = paramName.Contains('.') ? paramName.Substring(paramName.IndexOf('.') + 1) : paramName;

        switch (paramSource)
        {
            case "query":
                if (fullUrl != null)
                {
                    if (!hasQueryParameters)
                    {
                        fullUrl.Append("?");
                        hasQueryParameters = true;
                    }
                    else
                    {
                        fullUrl.Append("&");
                    }

                    fullUrl.Append($"{flatParamName}={defaultValue}");
                }

                break;
            case "header":
                formData.Add($"--header \"{flatParamName}: {defaultValue}\" ");
                break;
            default:
                formData.Add($"--data-urlencode '{flatParamName}={defaultValue}'");
                break;
        }
    }


    private string GetControllerRouteAnnotation(ApiGroup apiGroup)
    {
        if (apiGroup.Annotations == null) return ""; // Return empty if no annotations are present

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
        var httpMethodMatch = Regex.Match(annotations, "(Http(Get|Post|Put|Delete|Patch|Head|Options|Trace|Connect))",
            RegexOptions.IgnoreCase);
        if (httpMethodMatch.Success)
        {
            var httpMethodAnnotation =
                httpMethodMatch.Groups[2].Value; // Capture only the method part (GET, POST, etc.)
            return httpMethodAnnotation.ToUpper();
        }

        return "GET"; // Default to GET if no specific HTTP method annotation is found
    }

    private bool IsJsonString(string value)
    {
        value = value.Trim();
        return (value.StartsWith("{") && value.EndsWith("}")) || (value.StartsWith("[") && value.EndsWith("]"));
    }
}