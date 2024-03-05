using HealthMonitorApp.Data;
using HealthMonitorApp.Models;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace HealthMonitorApp.Services;

public class RepositoryService
{
    private readonly ApplicationDbContext _dbContext;
    private readonly IWebHostEnvironment _env;


    public RepositoryService(ApplicationDbContext dbContext, IWebHostEnvironment env)
    {
        _dbContext = dbContext;
        _env = env;
    }


    public async Task<RepositoryAnalysis?> GetRepositoryAnalysisByUrlAsync(string repositoryUrl)
    {
        return await _dbContext.RepositoryAnalysis.FirstOrDefaultAsync(ra => ra.Url == repositoryUrl);
    }

    public async Task<RepositoryAnalysis?> GetRepositoryAnalysisByNameAsync(string repositoryName)
    {
        return await _dbContext.RepositoryAnalysis.FirstOrDefaultAsync(ra => ra.Name == repositoryName);
    }


    public async Task<List<RepositoryAnalysis?>> GetAllRepositoryAnalysis()
    {
        return await _dbContext.RepositoryAnalysis.ToListAsync();
    }

    public async Task<RepositoryAnalysis?> GetRepositoryAnalysisByIdAsync(int id)
    {
        return await _dbContext.RepositoryAnalysis.FindAsync(id);
    }

    public async Task SaveRepositoryAnalysisAsync(RepositoryAnalysis repositoryAnalysis)
    {
        _dbContext.RepositoryAnalysis.Add(repositoryAnalysis);
        await _dbContext.SaveChangesAsync();
    }

    public async Task UpdateRepositoryAnalysisAsync(RepositoryAnalysis repositoryAnalysis)
    {
        _dbContext.RepositoryAnalysis.Update(repositoryAnalysis);
        await _dbContext.SaveChangesAsync();
    }

    public async Task DeleteRepositoryAnalysisAsync(RepositoryAnalysis repositoryAnalysis)
    {
        _dbContext.RepositoryAnalysis.Remove(repositoryAnalysis);
        await _dbContext.SaveChangesAsync();
    }

    public async Task<string> ExtractControllersAndEndpointsAsJsonAsync(RepositoryAnalysis? repositoryAnalysis)
    {
        var apiGroups = new List<ApiGroup>();
        var allFiles = Directory.GetFiles(repositoryAnalysis.Path, "*.cs", SearchOption.AllDirectories);
        var compilation = await CreateCompilationAsync(allFiles);

        foreach (var tree in compilation.SyntaxTrees)
        {
            var semanticModel = compilation.GetSemanticModel(tree);
            var root = tree.GetRoot();

            var controllers = root.DescendantNodes().OfType<ClassDeclarationSyntax>()
                .Where(node => node.BaseList?.Types.Any(type => type.ToString().Contains("Controller")) ?? false);

            foreach (var controller in controllers)
            {
                var controllerAttributes = controller.AttributeLists.SelectMany(attrList => attrList.Attributes);
                var controllerAnnotationsListed = string.Join(", ", controllerAttributes);
                var isControllerAuthorized =
                    controllerAttributes.Any(attr => attr.Name.ToString().Contains("Authorize"));

                var apiGroup = new ApiGroup
                {
                    Name = controller.Identifier.Text,
                    IsAuthorized = isControllerAuthorized,
                    Annotations = controllerAnnotationsListed
                };

                foreach (var method in controller.Members.OfType<MethodDeclarationSyntax>())
                {
                    if (!IsLikelyApiEndpoint(method)) continue;
                    var methodAttributes = method.AttributeLists.SelectMany(attrList => attrList.Attributes);
                    var methodAnnotationsListed = string.Join(" , ", methodAttributes);
                    var isMethodAuthorized = methodAttributes.Any(attr => attr.Name.ToString().Contains("Authorize")) ||
                                             isControllerAuthorized;
                    var isMethodOpen = methodAttributes.Any(attr => attr.Name.ToString().Contains("AllowAnonymous"));

                    var apiEndPoint = new ApiEndpoint
                    {
                        Name = method.Identifier.Text,
                        IsAuthorized = isMethodAuthorized,
                        IsOpen = !isMethodAuthorized || isMethodOpen,
                        Annotations = methodAnnotationsListed
                    };

                    apiGroup.ApiEndpoints.Add(apiEndPoint);
                }

                apiGroups.Add(apiGroup);
            }
        }

        var json = JsonConvert.SerializeObject(apiGroups, Formatting.Indented);
        return json;
    }

    private async Task<Compilation> CreateCompilationAsync(string[] filePaths)
    {
        var syntaxTrees = new List<SyntaxTree>();
        var options = CSharpParseOptions.Default.WithLanguageVersion(LanguageVersion.CSharp9);

        foreach (var path in filePaths)
        {
            var code = await File.ReadAllTextAsync(path);
            syntaxTrees.Add(CSharpSyntaxTree.ParseText(code, options));
        }

        var references = new List<MetadataReference>
        {
            MetadataReference.CreateFromFile(typeof(object).Assembly.Location)
            // Add other necessary references
        };

        return CSharpCompilation.Create("Analysis", syntaxTrees, references);
    }

    private bool IsLikelyApiEndpoint(MethodDeclarationSyntax method)
    {
        if (!method.Modifiers.Any(SyntaxKind.PublicKeyword)) return false;

        // Extended to include PUT, PATCH, DELETE, OPTIONS alongside GET and POST
        var hasHttpOrRouteAttribute = method.AttributeLists.Any(attrList =>
            attrList.Attributes.Any(attr =>
                new[] { "HttpGet", "HttpPost", "HttpPut", "HttpPatch", "HttpDelete", "HttpOptions" }
                    .Any(httpMethod => attr.Name.ToString().EndsWith(httpMethod)) ||
                attr.Name.ToString().Contains("Http") ||
                attr.Name.ToString().Contains("Route")));

        // Inspecting return types more comprehensively
        var hasApiReturnType = method.ReturnType.ToString().Contains("ActionResult") ||
                               method.ReturnType.ToString().Contains("IActionResult") ||
                               method.ReturnType.ToString().Contains("Task") ||
                               method.ReturnType.ToString().Contains("JsonResult");

        // Inspecting method parameters for typical API attributes
        var hasApiParameterAttributes = method.ParameterList.Parameters.Any(parameter =>
            parameter.AttributeLists.Any(attrList =>
                attrList.Attributes.Any(attr =>
                    new[] { "FromBody", "FromQuery", "FromRoute", "FromHeader", "FromForm" }
                        .Any(paramAttr => attr.Name.ToString().Contains(paramAttr)))));


        return hasHttpOrRouteAttribute || hasApiReturnType || hasApiParameterAttributes;
    }


    public async Task<string> GetCombinedApiPrefixAsync(RepositoryAnalysis repositoryAnalysis)
    {
        // First, attempt to extract the API prefix directly from code (Startup.cs or Program.cs).
        var apiPrefixFromCode = await GetApiPrefixFromCodeAsync(repositoryAnalysis);

        // If a prefix was successfully found in the code, return it.
        if (!string.IsNullOrEmpty(apiPrefixFromCode)) return apiPrefixFromCode;

        // If no prefix was found in the code, attempt to extract it from appsettings.json.
        var apiPrefixFromAppSettings = GetApiPrefixFromAppSettings(repositoryAnalysis);

        // Return the prefix found in appsettings.json, or an empty string if none was found.
        return apiPrefixFromAppSettings ?? string.Empty;
    }


    private async Task<string> GetApiPrefixFromCodeAsync(RepositoryAnalysis repositoryAnalysis)
    {
        var projectPath = repositoryAnalysis.Path;
        // Define paths for Startup.cs and Program.cs based on the project's structure.
        var startupPath = Path.Combine(projectPath, "Startup.cs");
        var programPath = Path.Combine(projectPath, "Program.cs");

        var filePaths = new List<string>();
        if (File.Exists(startupPath)) filePaths.Add(startupPath);
        if (File.Exists(programPath)) filePaths.Add(programPath);

        var syntaxTrees = new List<SyntaxTree>();
        foreach (var path in filePaths)
        {
            var code = await File.ReadAllTextAsync(path);
            syntaxTrees.Add(CSharpSyntaxTree.ParseText(code));
        }

        var compilation = CSharpCompilation.Create("RouteAnalysis", syntaxTrees);

        foreach (var tree in syntaxTrees)
        {
            var semanticModel = compilation.GetSemanticModel(tree);
            var root = tree.GetRoot();

            // Look for UseEndpoints or MapControllers method calls.
            var invocationExpressions = root.DescendantNodes().OfType<InvocationExpressionSyntax>();

            foreach (var invocation in invocationExpressions)
            {
                var expression = invocation.Expression.ToString();
                if (expression.Contains("UseEndpoints") || expression.Contains("MapControllers"))
                {
                    var argumentList = invocation.DescendantNodes().OfType<ArgumentListSyntax>().FirstOrDefault();
                    if (argumentList != null)
                    {
                        // Attempt to find a route prefix specified in the MapControllerRoute or similar method.
                        var lambdaExpression = argumentList.DescendantNodes().OfType<SimpleLambdaExpressionSyntax>()
                            .FirstOrDefault();
                        var mapRouteInvocation = lambdaExpression?.Body.DescendantNodes()
                            .OfType<InvocationExpressionSyntax>()
                            .FirstOrDefault(inv => inv.Expression.ToString().Contains("MapControllerRoute"));

                        if (mapRouteInvocation != null)
                        {
                            var routeTemplateArg = mapRouteInvocation.ArgumentList.Arguments.Skip(1).FirstOrDefault();
                            if (routeTemplateArg != null)
                            {
                                var routePrefix = routeTemplateArg.Expression.ToString().Trim('"');
                                return routePrefix; // Found a route prefix
                            }
                        }
                    }
                }
            }
        }

        return string.Empty; // No specific route prefix found
    }

    private string GetApiPrefixFromAppSettings(RepositoryAnalysis repositoryAnalysis)
    {
        var projectPath = repositoryAnalysis.Path;
        var appSettingsPath = Path.Combine(projectPath, "appsettings.json");
        if (File.Exists(appSettingsPath))
        {
            var appSettingsText = File.ReadAllText(appSettingsPath);
            var appSettingsJson = JObject.Parse(appSettingsText);

            // Assuming your route prefix is stored in a specific key, e.g., "ApiSettings:RoutePrefix"
            var routePrefix = appSettingsJson.SelectToken("ApiSettings.RoutePrefix")?.ToString();
            return routePrefix ?? string.Empty;
        }

        return string.Empty;
    }


    public string GetDynamicRepositoryStoragePath(string modelName, string branchName = "master")
    {
        string basePath;

        if (IsRunningInContainer())
            // For Docker or containerized environments, use a path that's expected to be a volume mount.
            basePath = "/var/appdata";
        else if (IsRunningInCloudEnvironment())
            // For cloud environments like Azure or AWS, you might decide based on environment variables.
            basePath = Environment.GetEnvironmentVariable("CLOUD_STORAGE_PATH") ??
                       Path.Combine(_env.ContentRootPath, "Data");
        else
            // For local development or unsupported environments, use a local path relative to the content root.
            basePath = Path.Combine(_env.ContentRootPath, "Data");


        var parentDirectory = Directory.GetParent(_env.ContentRootPath)?.Parent?.FullName;
        var repositoryDownloadPath = Path.Combine(parentDirectory, "Repos", modelName, branchName);
        return repositoryDownloadPath;
    }

    private bool IsRunningInContainer()
    {
        // A simple heuristic to check if running inside a Docker container is to check for the .dockerenv file.
        return File.Exists("/.dockerenv");
    }

    private bool IsRunningInCloudEnvironment()
    {
        // Implement checks for cloud environments. This can be based on specific environment variables
        // that cloud providers set, for example, WEBSITE_INSTANCE_ID for Azure App Services.
        return !string.IsNullOrEmpty(Environment.GetEnvironmentVariable("WEBSITE_INSTANCE_ID")) ||
               !string.IsNullOrEmpty(Environment.GetEnvironmentVariable("AWS_EXECUTION_ENV"));
    }
}