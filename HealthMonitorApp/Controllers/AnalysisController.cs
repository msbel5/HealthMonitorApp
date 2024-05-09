using HealthMonitorApp.Data;
using HealthMonitorApp.Models;
using HealthMonitorApp.Services;
using HealthMonitorApp.Tools;
using HealthMonitorApp.ViewModels;
using iTextSharp.text;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace HealthMonitorApp.Controllers;

using Microsoft.AspNetCore.Mvc.Rendering;

public class AnalysisController : Controller
{
    private readonly DataSeeder _dataSeeder;
    private readonly ApplicationDbContext _dbContext;
    private readonly ApplicationInspectorService _inspectorService;
    private readonly RepositoryService _repositoryService;
    private readonly VcsService _vcsService;
    private readonly ILogger<AnalysisController> _logger;



    public AnalysisController(ApplicationInspectorService inspectorService, VcsService vcsService,
        ApplicationDbContext dbContext, DataSeeder dataSeeder,
        RepositoryService repositoryService, ILogger<AnalysisController> logger)
    {
        _inspectorService = inspectorService;
        _vcsService = vcsService;
        _dbContext = dbContext;
        _dataSeeder = dataSeeder;
        _repositoryService = repositoryService;
        _logger = logger;
    }


    [HttpPost]
    public async Task<IActionResult> AnalyzeRepository(string repositoryUrl)
    {
        _logger.LogInformation("Analyzing repository: {RepositoryUrl}", repositoryUrl);
        // Assume a model or method to fetch or create a RepositoryAnalysis object from the URL
        var repositoryAnalysis =
            await _inspectorService.AnalyzeRepositoryAsync(new RepositoryAnalysis { Url = repositoryUrl });

        // Save or update analysis result in database as needed

        return View("AnalysisResult", repositoryAnalysis);
    }
    
    

    [HttpGet]
    public IActionResult BaseMethod()
    {
        //this is a base method for testing excluding functions
        throw new NotImplementedException();
    }



    [HttpGet]
    public IActionResult Create()
    {
        var model = new RepositoryCreateViewModel
        {
            // Populate the ApiGroups property
            ApiGroups = _dbContext.ApiGroups.Select(g => new SelectListItem 
            { 
                Value = g.Id.ToString(), 
                Text = g.Name 
            }).ToList()
        };
        _logger.LogInformation("Create method called");

        return View(model);
    }

    [HttpPost]
    public async Task<IActionResult> Create(RepositoryCreateViewModel model)
    {
        if (ModelState.IsValid)
        {
            _logger.LogInformation("Create method called with valid model");
            // Check for existing repository by URL or name 
            var nameExists = _dbContext.RepositoryAnalysis.Any(ra => ra.Name == model.Name);
            if (nameExists)
            {
                ModelState.AddModelError("", "This repository name already exists.");
                _logger.LogError("Repository name already exists");
                return View(model);
            }

            var urlExists = _dbContext.RepositoryAnalysis.Any(ra => ra.Url == model.Url);
            if (urlExists)
            {
                // Optionally, check if a different branch for the same URL is being added
                var branchExists =
                    _dbContext.RepositoryAnalysis.Any(ra => ra.Url == model.Url && ra.Branch == model.Branch);
                if (branchExists)
                {
                    ModelState.AddModelError("", "This repository and branch combination already exists.");
                    _logger.LogError("Repository and branch combination already exists");
                    return View(model);
                }
            }


            var repositoryDownloadPath =
                _repositoryService.GetDynamicRepositoryStoragePath(model.Name, model.Branch ?? "master");
            _logger.LogInformation("Repository download path: {RepositoryDownloadPath}", repositoryDownloadPath);
            
            if (!Directory.Exists(repositoryDownloadPath))
            {
                Directory.CreateDirectory(repositoryDownloadPath);
                _logger.LogInformation("Repository download path created");
            }
            


            var newRepositoryAnalysis = new RepositoryAnalysis
            {
                Name = model.Name,
                Url = model.Url,
                Branch = model.Branch ?? "master", // Default to 'master' if not specified
                Path = repositoryDownloadPath,
                BaseUrl = model.BaseUrl
            };

            if (model.Username != null && model.Password != null)
                newRepositoryAnalysis.EncryptCredentials(model.Username, model.Password);

            if (model.ExcludedControllers != null)
                newRepositoryAnalysis.ExcludedControllers = model.ExcludedControllers;

            if (model.ExcludedMethods != null)
                newRepositoryAnalysis.ExcludedEndpoints = model.ExcludedMethods;

            await _vcsService.DownloadRepositoryAsync(newRepositoryAnalysis);
            newRepositoryAnalysis = await _inspectorService.AnalyzeAsync(newRepositoryAnalysis);
            await _repositoryService.CreateExcelFromRepositoryAsync(newRepositoryAnalysis);


            if (model.SelectedApiGroupIds != null && model.SelectedApiGroupIds.Count > 0)
            {
                foreach (var groupId in model.SelectedApiGroupIds)
                {
                    var apiGroup = await _dbContext.ApiGroups.FindAsync(groupId);
                    if (apiGroup != null)
                    {
                        newRepositoryAnalysis.ApiGroups.Add(apiGroup);
                    }
                }
            }
            _dbContext.RepositoryAnalysis.Add(newRepositoryAnalysis);
            await _dbContext.SaveChangesAsync();

            var variables = model.Variables;
            if (variables != null && variables.Count > 0)
            {
                foreach (Variable variable in variables)
                {
                    var var = new Variable
                    {
                        Name = variable.Name,
                        Value = Variable.EncryptVariable(variable.Value),
                        RepositoryAnalysisId = newRepositoryAnalysis.Id,
                    };
                    _dbContext.Variables.Add(var);
                    await _dbContext.SaveChangesAsync();
                }
            }
            
            if (model.IntegrateEndpoints) await _dataSeeder.SeedDataFromRepository(newRepositoryAnalysis);
            return RedirectToAction("Index");
        }
        
        foreach (var error in ViewData.ModelState.Values.SelectMany(modelState => modelState.Errors))
        {
            // Log or inspect the error message
            _logger.LogError(error.ErrorMessage);
        }
        
        model.ApiGroups = _dbContext.ApiGroups.Select(g => new SelectListItem
        {
            Value = g.Id.ToString(),
            Text = g.Name
        }).ToList();
        
        // If model state is not valid, show the form again with validation messages
        return View(model);
    }


    public IActionResult Index()
    {
        // Fetch all repositories from the database
        var repositories = _dbContext.RepositoryAnalysis.ToList();
        return View(repositories);
    }


    // Initiates analysis on all repositories
    public IActionResult CheckAllRepositories()
    {
        // Add logic to trigger analysis for all repositories
        return RedirectToAction("Index");
    }

    // Displays details of a specific repository
    public IActionResult Details(Guid id)
    {
        var repository = _dbContext.RepositoryAnalysis.Find(id);
        return View(repository);
    }

    // Displays the form for editing a repository's details
    public IActionResult Edit(Guid id)
    {
        // Fetch repository by id for editing
        var repository = _dbContext.RepositoryAnalysis.Find(id); // Replace with actual fetch logic
        return View(repository);
    }

    [HttpPost]
    public IActionResult Edit(RepositoryAnalysis model)
    {
        if (ModelState.IsValid)
            // Add logic to update the repository details
            return RedirectToAction("Index");
        return View(model);
    }

    // Displays confirmation for deleting a repository
    public async Task<IActionResult> Delete(Guid id)
    {
        var repositoryAnalysis = await _repositoryService.GetRepositoryAnalysisByIdAsync(id);

        if (repositoryAnalysis == null) return NotFound();
        

        return PartialView("_DeleteConfirmation", repositoryAnalysis);
    }

    [HttpPost]
    [ActionName("Delete")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteConfirmed(Guid id)
    {
        var repositoryAnalysis = await _repositoryService.GetRepositoryAnalysisByIdAsync(id);
        if (repositoryAnalysis == null) return NotFound();

        await _vcsService.DeleteRepositoryAsync(repositoryAnalysis);
        List<ApiGroup> apiGroups = repositoryAnalysis.ApiGroups.ToList();
        List<ApiEndpoint> apiEndpoints = repositoryAnalysis.ApiGroups.SelectMany(ag => ag.ApiEndpoints).ToList();
        List<ServiceStatus> serviceStatuses = repositoryAnalysis.ApiGroups.SelectMany(ag => ag.ApiEndpoints)
            .Select(ae => ae.ServiceStatus).ToList();
        List<Variable> variables = _dbContext.Variables.Where(v => v.RepositoryAnalysisId == id).ToList();
        _dbContext.RemoveRange(serviceStatuses);
        _dbContext.RemoveRange(apiEndpoints);
        _dbContext.RemoveRange(apiGroups);
        _dbContext.RemoveRange(variables);
        _dbContext.RepositoryAnalysis.Remove(repositoryAnalysis);
        await _dbContext.SaveChangesAsync();
        return RedirectToAction(nameof(Index));
    }


    public IActionResult GetReport(Guid id)
    {
        var repositoryAnalysis = _dbContext.RepositoryAnalysis.FirstOrDefault(ra => ra.Id == id);
        if (repositoryAnalysis == null) return NotFound();

        var reportPath = repositoryAnalysis.GetReportPath(); // Ensure this returns the path to your HTML report
        var content = System.IO.File.ReadAllText(reportPath);
        return Content(content, "text/html");
    }
    
    public IActionResult GetReportExcel(Guid id)
    {
        var repositoryAnalysis = _dbContext.RepositoryAnalysis.FirstOrDefault(ra => ra.Id == id);
        if (repositoryAnalysis == null) return NotFound();

        var reportPath = repositoryAnalysis.GetExcelPath();

        // Ensure the file exists
        if (!System.IO.File.Exists(reportPath)) return NotFound("Report not found.");

        var content = System.IO.File.ReadAllBytes(reportPath); // Read as byte array
        return File(content, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", Path.GetFileName(reportPath)); 
    }

}