using System.Drawing;
using HealthMonitorApp.Data;
using HealthMonitorApp.Models;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using OfficeOpenXml;
using OfficeOpenXml.Style;
using OfficeOpenXml.Table;

namespace HealthMonitorApp.Services;

public class RepositoryService
{
    private readonly ApplicationDbContext _dbContext;
    private readonly IWebHostEnvironment _env;
    private readonly Logger<RepositoryService> _logger;


    public RepositoryService(ApplicationDbContext dbContext, IWebHostEnvironment env)
    {
        _dbContext = dbContext;
        _env = env;
        _logger = new Logger<RepositoryService>(new LoggerFactory());
    }


    public async Task<RepositoryAnalysis?> GetRepositoryAnalysisByUrlAsync(string repositoryUrl)
    {
        _logger.LogInformation("Getting repository analysis by URL: {RepositoryUrl}", repositoryUrl);
        return await _dbContext.RepositoryAnalysis.FirstOrDefaultAsync(ra => ra.Url == repositoryUrl);
    }

    public async Task<RepositoryAnalysis?> GetRepositoryAnalysisByNameAsync(string repositoryName)
    {
        _logger.LogInformation("Getting repository analysis by name: {RepositoryName}", repositoryName);
        return await _dbContext.RepositoryAnalysis.FirstOrDefaultAsync(ra => ra.Name == repositoryName);
    }


    public async Task<List<RepositoryAnalysis?>> GetAllRepositoryAnalysis()
    {
        _logger.LogInformation("Getting all repository analysis");
        return await _dbContext.RepositoryAnalysis.ToListAsync();
    }

    public async Task<RepositoryAnalysis?> GetRepositoryAnalysisByIdAsync(Guid id)
    {
        _logger.LogInformation("Getting repository analysis by ID: {RepositoryId}", id);
        return await _dbContext.RepositoryAnalysis
            .Include(ra => ra.ApiGroups)
            .ThenInclude(ag => ag.ApiEndpoints)
            .ThenInclude(ae => ae.ServiceStatus)
            .FirstOrDefaultAsync(g => g.Id == id);
    }

    public async Task SaveRepositoryAnalysisAsync(RepositoryAnalysis repositoryAnalysis)
    {
        _dbContext.RepositoryAnalysis.Add(repositoryAnalysis);
        await _dbContext.SaveChangesAsync();
        _logger.LogInformation("Saved repository analysis: {RepositoryName}", repositoryAnalysis.Name);
    }

    public async Task UpdateRepositoryAnalysisAsync(RepositoryAnalysis repositoryAnalysis)
    {
        _dbContext.RepositoryAnalysis.Update(repositoryAnalysis);
        await _dbContext.SaveChangesAsync();
        _logger.LogInformation("Updated repository analysis: {RepositoryName}", repositoryAnalysis.Name);
    }

    public async Task DeleteRepositoryAnalysisAsync(RepositoryAnalysis repositoryAnalysis)
    {
        _dbContext.RepositoryAnalysis.Remove(repositoryAnalysis);
        await _dbContext.SaveChangesAsync();
        _logger.LogInformation("Deleted repository analysis: {RepositoryName}", repositoryAnalysis.Name);
    }

    public async Task CreateExcelFromRepositoryAsync(RepositoryAnalysis? repositoryAnalysis)
    {
        var json = await ExtractControllersAndEndpointsAsJsonAsync(repositoryAnalysis);
        var apiGroups = JsonConvert.DeserializeObject<List<ApiGroup>>(json);
        if (apiGroups == null) return;

        ExcelPackage.LicenseContext = LicenseContext.NonCommercial;
        using var package = new ExcelPackage();
        var worksheet = package.Workbook.Worksheets.Add("API Endpoints");

        // Set up the headers
        worksheet.Cells[1, 1].Value = "Controller Name";
        worksheet.Cells[1, 2].Value = "Endpoint Name";
        worksheet.Cells[1, 3].Value = "Needs Token";
        worksheet.Cells[1, 4].Value = "Is Authorized";
        worksheet.Cells[1, 5].Value = "Is Open";
        worksheet.Cells[1, 6].Value = "Annotations";

        var currentRow = 2;
        foreach (var group in apiGroups)
        foreach (var endpoint in group.ApiEndpoints)
        {
            var needToken = (endpoint.IsAuthorized ?? false) || !(endpoint.IsOpen ?? true);

            worksheet.Cells[currentRow, 1].Value = group.Name;
            worksheet.Cells[currentRow, 2].Value = endpoint.Name;
            worksheet.Cells[currentRow, 3].Value = needToken ? "Yes" : "No";
            worksheet.Cells[currentRow, 4].Value = endpoint.IsAuthorized == true ? "Yes" : "No";
            worksheet.Cells[currentRow, 5].Value = endpoint.IsOpen == true ? "Yes" : "No";
            worksheet.Cells[currentRow, 6].Value = endpoint.Annotations;
            currentRow++;
        }

        // Convert range to table for sortable columns
        var tableName = "ApiEndpointsTable";
        var tableRange = worksheet.Cells[1, 1, currentRow - 1, 6];
        var table = worksheet.Tables.Add(tableRange, tableName);
        table.ShowHeader = true;
        table.ShowFilter = true;
        table.TableStyle = TableStyles.Medium2;

        // Auto-fit columns
        worksheet.Cells[worksheet.Dimension.Address].AutoFitColumns();

        // Apply conditional formatting for the entire row based on "Needs Token" column
        for (var row = 2; row < currentRow; row++)
        {
            var rowRange = worksheet.Cells[row, 1, row, 6];
            var condition = worksheet.ConditionalFormatting.AddExpression(rowRange);
            condition.Formula = $"$C{row}=\"Yes\"";
            condition.Style.Fill.PatternType = ExcelFillStyle.Solid;
            var needToken = string.Equals(worksheet.Cells[row, 3].Value.ToString(), "Yes",
                StringComparison.OrdinalIgnoreCase);
            condition.Style.Fill.BackgroundColor.Color =
                needToken ? Color.LightGreen : Color.LightCoral;
        }

        // Save the Excel file
        var filePath = repositoryAnalysis?.GetExcelPath();
        if (filePath != null)
        {
            var fileInfo = new FileInfo(filePath);
            await package.SaveAsAsync(fileInfo);
        }

        _logger.LogInformation("Created Excel file for repository: {RepositoryName}", repositoryAnalysis?.Name);
    }


    public async Task<string> ExtractControllersAndEndpointsAsJsonAsync(RepositoryAnalysis? repositoryAnalysis)
    {
        var apiGroups = new List<ApiGroup>();
        var allFiles = Directory.GetFiles(repositoryAnalysis.Path, "*.cs", SearchOption.AllDirectories);
        var compilation = await CreateCompilationAsync(allFiles);
        _logger.LogInformation("Extracting controllers and endpoints from {FileCount} files", allFiles.Length);

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

                _logger.LogInformation("Found API group: {GroupName}", apiGroup.Name);

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
                        Annotations = methodAnnotationsListed,
                        Parameters = JsonConvert.SerializeObject(ExtractMethodParameters(method, semanticModel),
                            Formatting.None)
                    };

                    apiGroup.ApiEndpoints.Add(apiEndPoint);

                    _logger.LogInformation("Found API endpoint: {EndpointName} on {ApiGroup}", apiEndPoint.Name,
                        apiGroup.Name);
                }

                apiGroups.Add(apiGroup);
            }
        }

        var excludedControllers = repositoryAnalysis.ExcludedControllers;
        List<string> excludedControllersList = new();
        if (excludedControllers != null)
            excludedControllersList = excludedControllers
                .Split(new[] { ',', ' ' }, StringSplitOptions.RemoveEmptyEntries)
                .Select(s => s.Trim())
                .ToList();
        apiGroups = apiGroups.Where(c =>
            !excludedControllersList.Any(ec => c.Name.Contains(ec, StringComparison.OrdinalIgnoreCase))).ToList();

        var excludedEndpoints = repositoryAnalysis.ExcludedEndpoints;
        List<string> excludedEndpointsList = new();
        if (excludedEndpoints != null)
            excludedEndpointsList = excludedEndpoints.Split(new[] { ',', ' ' }, StringSplitOptions.RemoveEmptyEntries)
                .Select(s => s.Trim())
                .ToList();
        foreach (var apiGroup in apiGroups)
            apiGroup.ApiEndpoints = apiGroup.ApiEndpoints.Where(e =>
                !excludedEndpointsList.Any(ee => e.Name.Contains(ee, StringComparison.OrdinalIgnoreCase))).ToList();


        var json = JsonConvert.SerializeObject(apiGroups, Formatting.None);
        _logger.LogInformation("Extracted {ApiGroupCount} API groups and {ApiEndpointCount} endpoints", apiGroups.Count,
            apiGroups.Sum(g => g.ApiEndpoints.Count));
        _logger.LogDebug("Extracted API groups and endpoints: {Json}", json);
        return json;
    }


    private JArray ExtractMethodParameters(MethodDeclarationSyntax method, SemanticModel model)
    {
        var parameters = new JArray();
        foreach (var parameter in method.ParameterList.Parameters)
        {
            var typeInfo = model.GetTypeInfo(parameter.Type);
            var paramObj = new JObject
            {
                ["Name"] = parameter.Identifier.Text,
                ["Type"] = typeInfo.Type?.ToString(),
                ["Source"] = DetermineParameterSource(parameter),
                ["DefaultValue"] = GetDefaultValueForType(typeInfo.Type, model)
            };

            // Check if the parameter is a complex type
            if (typeInfo.Type is INamedTypeSymbol namedTypeSymbol && !namedTypeSymbol.IsValueType &&
                namedTypeSymbol.Name != "String")
            {
                paramObj["IsComplex"] = true;
                paramObj["Properties"] = ExtractTopLevelProperties(namedTypeSymbol, model);
            }

            parameters.Add(paramObj);
        }

        return parameters;
    }

    private JArray ExtractTopLevelProperties(INamedTypeSymbol typeSymbol, SemanticModel model)
    {
        var properties = new JArray();
        foreach (var property in typeSymbol.GetMembers().OfType<IPropertySymbol>())
            if (property.DeclaredAccessibility == Accessibility.Public && !property.Type.ToString().Contains("?") &&
                !IsCollectionType(property.Type))
                properties.Add(new JObject
                {
                    ["Name"] = property.Name,
                    ["Type"] = property.Type.ToString(),
                    ["DefaultValue"] = GetDefaultValueForType(property.Type, model)
                });
        return properties;
    }


    private string DetermineParameterSource(ParameterSyntax parameter)
    {
        var sourceAttribute = parameter.AttributeLists
            .SelectMany(a => a.Attributes)
            .FirstOrDefault(a =>
                new[] { "FromBody", "FromQuery", "FromRoute", "FromForm" }.Any(tag => a.Name.ToString().Contains(tag)));

        return sourceAttribute?.Name.ToString().Replace("From", "").ToLower() ?? "query";
    }


    private JArray ExtractPropertiesFromComplexType(INamedTypeSymbol typeSymbol, SemanticModel model)
    {
        var properties = new JArray();
        foreach (var property in typeSymbol.GetMembers().OfType<IPropertySymbol>())
        {
            var propertyObj = new JObject
            {
                ["Name"] = property.Name,
                ["Type"] = property.Type.ToString(),
                ["DefaultValue"] = GetDefaultValueForType(property.Type, model)
            };

            // Recursively extract properties if the property itself is a complex type
            if (property.Type is INamedTypeSymbol nestedTypeSymbol && !nestedTypeSymbol.IsValueType &&
                nestedTypeSymbol.Name != "String")
            {
                propertyObj["IsComplex"] = true;
                propertyObj["Properties"] = ExtractPropertiesFromComplexType(nestedTypeSymbol, model);
            }

            properties.Add(propertyObj);
        }

        return properties;
    }


    private string GetDefaultValueForType(ITypeSymbol type, SemanticModel model)
    {
        switch (type.SpecialType)
        {
            case SpecialType.System_String:
                return "placeholder";
            case SpecialType.System_Int32:
            case SpecialType.System_Decimal:
                return "200";
            case SpecialType.System_Boolean:
                return "false";
            case SpecialType.System_DateTime:
                return "2024-01-01T00:00:00Z";
            default:
                if (type.ToString() == "System.Guid")
                    return "00000000-0000-0000-0000-00000000000";
                if (type.TypeKind == TypeKind.Enum)
                {
                    var enumType = type as INamedTypeSymbol;
                    var firstEnumMember = enumType.GetMembers().OfType<IFieldSymbol>()
                        .FirstOrDefault(e => e.IsStatic && e.HasConstantValue);
                    return firstEnumMember != null ? "" + firstEnumMember.ConstantValue + "" : "";
                }

                if (IsComplexType(type) && IsEssentialComplexProperty(type, model))
                    return CreateSimplifiedObjectForViewModel(type as INamedTypeSymbol, model);
                return "placeholder";
        }
    }

// Helper to determine if a type is complex and essential
    private bool IsComplexType(ITypeSymbol type)
    {
        return type is INamedTypeSymbol namedType && !namedType.IsValueType && namedType.Name != "String";
    }

// Decide if a complex type property should be included based on its public nature and simplicity
    private bool IsEssentialComplexProperty(ITypeSymbol type, SemanticModel model)
    {
        var namedType = type as INamedTypeSymbol;
        if (namedType == null) return false;

        foreach (var property in namedType.GetMembers().OfType<IPropertySymbol>())
            // Check if the property is public, non-nullable, and not a collection
            if (property.DeclaredAccessibility == Accessibility.Public &&
                !property.Type.ToString().Contains("?") &&
                !IsCollectionType(property.Type))
                return true;
        return false;
    }

    // Check if the type is a collection
    private bool IsCollectionType(ITypeSymbol type)
    {
        return type.AllInterfaces.Any(i => i.Name == "IEnumerable") && type.Name != "String";
    }


    // Generate a simplified JSON object for essential complex types
    private string CreateSimplifiedObjectForViewModel(INamedTypeSymbol typeSymbol, SemanticModel model)
    {
        var simplifiedObject = new JObject();
        foreach (var property in typeSymbol.GetMembers().OfType<IPropertySymbol>())
            if (property.DeclaredAccessibility == Accessibility.Public &&
                !property.Type.ToString().Contains("?") &&
                !IsCollectionType(property.Type))
                simplifiedObject[property.Name] = GetDefaultValueForType(property.Type, model);
        return JsonConvert.SerializeObject(simplifiedObject, Formatting.None);
    }


    public async Task<Compilation> CreateCompilationAsync(string[] filePaths)
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
        _logger.LogInformation("Created compilation for {FileCount} files", filePaths.Length);
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

        _logger.LogInformation(
            "API prefix from code: {ApiPrefixFromCode}, API prefix from appsettings.json: {ApiPrefixFromAppSettings}",
            apiPrefixFromCode, apiPrefixFromAppSettings);
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
        {
            basePath = "/var/appdata";
            _logger.LogInformation("Running in a container, using path: {BasePath}", basePath);
        }
        else if (IsRunningInCloudEnvironment())
        {
            // For cloud environments like Azure or AWS, you might decide based on environment variables.
            basePath = Environment.GetEnvironmentVariable("CLOUD_STORAGE_PATH") ??
                       Path.Combine(_env.ContentRootPath, "Data");
            _logger.LogInformation("Running in a cloud environment, using path: {BasePath}", basePath);
        }
        else
            // For local development or unsupported environments, use a local path relative to the content root.
        {
            basePath = Path.Combine(_env.ContentRootPath, "Data");
            _logger.LogInformation("Running in a local environment, using path: {BasePath}", basePath);
        }

        var tempPath = Path.GetTempPath();
        var repositoryDownloadPath = Path.Combine(tempPath, "Repos", modelName, branchName);
        _logger.LogInformation("Using repository download path: {RepositoryDownloadPath}", repositoryDownloadPath);
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