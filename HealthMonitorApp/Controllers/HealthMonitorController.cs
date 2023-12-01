using System.Text.RegularExpressions;
using HealthMonitorApp.Data;
using HealthMonitorApp.Models;
using HealthMonitorApp.Services;
using HealthMonitorApp.ViewModels;
using Microsoft.AspNetCore.Mvc;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Emit;
using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json;
using Microsoft.CodeAnalysis.CSharp.Scripting;
using Microsoft.CodeAnalysis.Scripting;


namespace HealthMonitorApp.Controllers;

public class HealthMonitorController : Controller
{
    private readonly ApplicationDbContext _context;
    private readonly HealthCheckService _healthCheckService;
    private readonly ILogger<HealthMonitorController> _logger;

    public HealthMonitorController(ApplicationDbContext context, HealthCheckService healthCheckService, ILogger<HealthMonitorController> logger)
    {
        _context = context;
        _healthCheckService = healthCheckService;
        _logger = logger;
    }

    public async Task<IActionResult> CheckAllServicesHealth()
    {
        var serviceStatusList = _context.ServiceStatuses
            .Include(apiEndPoints => apiEndPoints.ApiEndpoint)
            .Include(apiEndPoints => apiEndPoints.ApiEndpoint.ApiGroup).ToList();
        foreach (var serviceStatus in serviceStatusList)
        {
            await _healthCheckService.CheckServiceStatusHealthAsync(serviceStatus);
        }

        return RedirectToAction("Index"); // Redirect to the dashboard or appropriate view
    }


    public IActionResult Index()
    {
        // Fetch the services and group them by ApiGroup
        var services = _context.ServiceStatuses
            .Include(ss => ss.ApiEndpoint)
            .ThenInclude(ae => ae.ApiGroup)
            .ToList();


        var viewModelList = new List<ServiceStatusIndexViewModel>();

        foreach (var service in services)
        {
            var historiesForService = _context.ServiceStatusHistories
                .Where(h => h.ServiceStatusID == service.ID)
                .OrderByDescending(h => h.CheckedAt)
                .ToList();

            var lastThreeHistories = historiesForService
                .Take(3)
                .Select(h => h.ResponseTime)
                .ToList();

            double averageResponseTime = 0; // Default value

            if (historiesForService.Any()) // Check if there are any histories for the service
            {
                averageResponseTime = historiesForService.Average(h => h.ResponseTime);
            }

            var viewModel = new ServiceStatusIndexViewModel
            {
                ID = service.ID,
                Name = service.Name,
                IsHealthy = service.IsHealthy,
                ApiGroupName = service.ApiEndpoint.ApiGroup.Name,
                CurrentResponseTime = service.ResponseTime, // Assuming ResponseTime is the property name in ServiceStatus
                LastThreeResponseTimes = lastThreeHistories,
                AverageResponseTime = averageResponseTime,
                LastCheck = service.CheckedAt
            };

            viewModelList.Add(viewModel);
        }

        viewModelList = viewModelList.OrderByDescending(s => !s.IsHealthy)
            .ThenBy(s => s.CurrentResponseTime < s.AverageResponseTime)
            .ThenBy(s => s.CurrentResponseTime).ToList();

        return View(viewModelList);
    }



    public IActionResult Create()
    {
        var viewModel = new ServiceStatusCreateViewModel
        {
            ApiGroups = _context.ApiGroups.ToList()

        };

        return View(viewModel);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(ServiceStatusCreateViewModel model)
    {
        _logger.LogInformation($"Creating service status with model: {JsonConvert.SerializeObject(model)}");

        if (ModelState.IsValid)
        {
            
            using (var transaction = await _context.Database.BeginTransactionAsync())
            {
                try
                {
                    ApiGroup apiGroup;

                    if (model.ApiGroupId == "addNew" && !string.IsNullOrEmpty(model.NewApiGroupName))
                    {
                        apiGroup = new ApiGroup { Name = model.NewApiGroupName };
                        _context.ApiGroups.Add(apiGroup);
                    }
                    else if (int.TryParse(model.ApiGroupId, out var groupId))
                    {
                        apiGroup = await _context.ApiGroups.FindAsync(groupId);
                    }
                    else
                    {
                        apiGroup = new ApiGroup { Name = ExtractApiGroupName(model.cURL) };
                        _context.ApiGroups.Add(apiGroup);
                    }

                    await _context.SaveChangesAsync();

                    var serviceStatus = new ServiceStatus
                    {
                        Name = model.ServiceName,
                    };

                    if (!string.IsNullOrWhiteSpace(model.AssertionScript)){
                        // Verify the Assertion Script
                        var scriptCheckResult = CheckCompilation(model.AssertionScript);
                        if (!scriptCheckResult.Success)
                        {
                            // Handle and return compilation errors
                            TempData["Error"] = "Compilation error in Assertion Script.";
                            return View(model);
                        }
                        serviceStatus.AssertionScript = model.AssertionScript;
                    }

                    _context.ServiceStatuses.Add(serviceStatus);
                    await _context.SaveChangesAsync();

                    var apiEndpoint = new ApiEndpoint
                    {
                        Name = ExtractApiName(model.cURL),
                        cURL = model.cURL,
                        ExpectedStatusCode = model.ExpectedStatusCode,
                        ApiGroupID = apiGroup.ID,
                        ServiceStatusID = serviceStatus.ID
                    };

                    _context.ApiEndpoints.Add(apiEndpoint);
                    await _context.SaveChangesAsync();
                    await transaction.CommitAsync();
                    await _healthCheckService.CheckServiceStatusHealthAsync(serviceStatus);
                    return RedirectToAction(nameof(Index));
                }
                catch (Exception ex)
                {
                    _logger.LogError($"Error: {ex.Message}");
                    await transaction.RollbackAsync();
                }
            }
        }

        _logger.LogWarning("Model state is not valid");
        return View(model);
    }

    private string ExtractApiGroupName(string cURL)
    {
        // Extracting the URL from the cURL command
        var match = Regex.Match(cURL, @"https?://[^/]+/(.*)");
        if (match.Success && match.Groups.Count > 1)
        {
            var urlPath = match.Groups[1].Value;
            var pathSegments = urlPath.Split('/', StringSplitOptions.RemoveEmptyEntries);

            // Determining the position of the API group name based on the URL structure
            if (pathSegments.Length >= 3 && (pathSegments[0].Equals("controller", StringComparison.OrdinalIgnoreCase) || pathSegments[0].Equals("api", StringComparison.OrdinalIgnoreCase)))
            {
                return pathSegments[1];
            }
            else if (pathSegments.Length >= 2)
            {
                return pathSegments[0];
            }
        }

        return "Default";  // Default group name if extraction fails; adapt as needed
    }

    private string ExtractApiName(string cURL)
    {
        // Extracting the URL from the cURL command
        var match = Regex.Match(cURL, @"https?://[^/]+/(.*)");
        if (match.Success && match.Groups.Count > 1)
        {
            var urlPath = match.Groups[1].Value;
            var pathSegments = urlPath.Split('/', StringSplitOptions.RemoveEmptyEntries);

            // Determining the position of the API name based on the URL structure
            if (pathSegments.Length >= 3 && (pathSegments[0].Equals("controller", StringComparison.OrdinalIgnoreCase) || pathSegments[0].Equals("api", StringComparison.OrdinalIgnoreCase)))
            {
                return pathSegments[2];
            }
            else if (pathSegments.Length >= 2)
            {
                return pathSegments[1];
            }
        }

        return "DefaultApiName";  // Default API name if extraction fails; adapt as needed
    }

    // GET: HealthMonitor/Edit/5
    public async Task<IActionResult> Edit(int? id)
    {
        if (id == null)
        {
            return NotFound();
        }

        var serviceStatus = await _context.ServiceStatuses
            .Include(s => s.ApiEndpoint)
            .FirstOrDefaultAsync(s => s.ID == id.Value);

        if (serviceStatus == null)
        {
            return NotFound();
        }

        var viewModel = new ServiceStatusEditViewModel()
        {
            ID = serviceStatus.ID,
            Name = serviceStatus.Name,
            ExpectedStatusCode = serviceStatus.ApiEndpoint.ExpectedStatusCode,
            CURL = serviceStatus.ApiEndpoint.cURL,
            ApiGroupID = serviceStatus.ApiEndpoint.ApiGroupID,
            ApiGroups = _context.ApiGroups.ToList(),
            AssertionScript = serviceStatus.AssertionScript
        };

        return View(viewModel);
    }


    // POST: HealthMonitor/Edit/5
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int id, [Bind("ID,Name,ExpectedStatusCode,CURL,AssertionScript,ApiGroupID,NewApiGroupName,ApiGroups")] ServiceStatusEditViewModel viewModel)
    {
        if (ModelState.IsValid)
        {
            try
            {
                if (viewModel.ApiGroupID == 0)
                {
                    if (string.IsNullOrEmpty(viewModel.NewApiGroupName))
                    {
                        ModelState.AddModelError("", "New API group name is required when adding a new API group.");
                        return View(viewModel);
                    }

                    var newApiGroup = new ApiGroup { Name = viewModel.NewApiGroupName };
                    _context.ApiGroups.Add(newApiGroup);
                    await _context.SaveChangesAsync();
                    viewModel.ApiGroupID = newApiGroup.ID;
                }

                // Assuming that the ID property of ServiceStatusEditViewModel is the ID of ServiceStatus
                var serviceStatus = await _context.ServiceStatuses.Include(s => s.ApiEndpoint)
                    .FirstOrDefaultAsync(s => s.ID == viewModel.ID);

                if (serviceStatus == null)
                {
                    return NotFound();
                }

                // Update the properties of serviceStatus and its ApiEndpoint based on viewModel
                serviceStatus.Name = viewModel.Name;
                serviceStatus.ApiEndpoint.ExpectedStatusCode = viewModel.ExpectedStatusCode;
                serviceStatus.ApiEndpoint.cURL = viewModel.CURL;
                serviceStatus.ApiEndpoint.ApiGroupID = viewModel.ApiGroupID;
                serviceStatus.AssertionScript = viewModel.AssertionScript;

                _context.Update(serviceStatus);
                await _context.SaveChangesAsync();
                await _healthCheckService.CheckServiceStatusHealthAsync(serviceStatus);
            }
            catch (DbUpdateConcurrencyException)
            {
                if (!ServiceStatusExists(viewModel.ID))  // Make sure to implement ServiceStatusExists method
                {
                    return NotFound();
                }
                else
                {
                    throw;
                }
            }
            return RedirectToAction(nameof(Index));
        }
        return View(viewModel);
    }

    private bool ServiceStatusExists(int id)
    {
        return _context.ServiceStatuses.Any(e => e.ID == id);
    }


    public async Task<IActionResult> Details(int id)
    {
        var serviceStatus = await _context.ServiceStatuses
            .Include(s => s.ApiEndpoint)
            .ThenInclude(a => a.ApiGroup)
            .FirstOrDefaultAsync(s => s.ID == id);

        if (serviceStatus == null)
        {
            return NotFound();
        }

        ViewBag.ApiGroups = await _context.ApiGroups.ToListAsync();
        var dynamicStringProcessor = new DynamicStringProcessor();
        serviceStatus.ApiEndpoint.cURL = dynamicStringProcessor.Process(serviceStatus.ApiEndpoint.cURL);
        return View(serviceStatus);
    }


    public IActionResult Delete(int id)
    {
        var serviceStatus = _context.ServiceStatuses.Find(id);
        if (serviceStatus == null)
        {
            return NotFound();
        }
        return PartialView("_DeleteConfirmation", serviceStatus);
    }

    [HttpPost, ActionName("Delete")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteConfirmed(int id)
    {
        var serviceStatus = await _context.ServiceStatuses.FindAsync(id);
        if (serviceStatus == null)
        {
            return NotFound();
        }

        // Find the associated ApiEndpoint
        var relatedEndpoint = _context.ApiEndpoints
            .Include(apiEndpoint => apiEndpoint.ApiGroup).FirstOrDefault(e => e.ServiceStatusID == id);

        // Remove all related ServiceStatusHistories
        var histories = _context.ServiceStatusHistories.Where(h => h.ServiceStatusID == serviceStatus.ID);
        _context.ServiceStatusHistories.RemoveRange(histories);

        // Remove the ServiceStatus
        _context.ServiceStatuses.Remove(serviceStatus);

        // Remove the ApiEndpoint itself
        if (relatedEndpoint != null)
        {
            _context.ApiEndpoints.Remove(relatedEndpoint);
        }

        await _context.SaveChangesAsync();

        // Check if the Auth and Group of the relatedEndpoint are orphaned and delete them if they are
        if (relatedEndpoint != null)
        {
            var isGroupOrphaned = !_context.ApiEndpoints.Any(e => e.ApiGroupID == relatedEndpoint.ApiGroupID);

            if (isGroupOrphaned && relatedEndpoint.ApiGroup != null)
            {
                _context.ApiGroups.Remove(relatedEndpoint.ApiGroup);
            }

            await _context.SaveChangesAsync();
        }

        return RedirectToAction(nameof(Index));
    }
    
    [HttpPost]
    public (bool Success, IEnumerable<string> Errors) CheckCompilation([FromBody] string script)
    {
        // Construct the full script with a static method
        string fullScript = $@"
                using System.Net.Http;
                public static class DynamicScript
                {{
                    public static bool AssertResponse(HttpResponseMessage response)
                    {{
                        {script}
                    }}
                }}";

        var syntaxTree = CSharpSyntaxTree.ParseText(fullScript);
        var references = new MetadataReference[]
        {
            MetadataReference.CreateFromFile(typeof(object).Assembly.Location),
            MetadataReference.CreateFromFile(typeof(HttpResponseMessage).Assembly.Location)
            // Add other necessary assemblies
        };

        var compilation = CSharpCompilation.Create("DynamicScriptAssembly")
            .WithOptions(new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary))
            .AddReferences(references)
            .AddSyntaxTrees(syntaxTree);

        using (var ms = new MemoryStream())
        {
            EmitResult result = compilation.Emit(ms);
            if (!result.Success)
            {
                var errors = result.Diagnostics
                    .Where(diagnostic => diagnostic.IsWarningAsError || diagnostic.Severity == DiagnosticSeverity.Error)
                    .Select(diagnostic => diagnostic.ToString());

                return (false, errors);
            }

            return (true, Enumerable.Empty<string>());

        }
    }

}
