using HealthMonitorApp.Data;
using HealthMonitorApp.Models;
using HealthMonitorApp.Services;
using HealthMonitorApp.Tools;
using HealthMonitorApp.ViewModels;
using iTextSharp.text;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace HealthMonitorApp.Controllers;

public class AnalysisController : Controller
{
    private readonly DataSeeder _dataSeeder;
    private readonly ApplicationDbContext _dbContext;
    private readonly ApplicationInspectorService _inspectorService;
    private readonly ReportHandler _reportHandler;
    private readonly RepositoryService _repositoryService;
    private readonly VcsService _vcsService;


    public AnalysisController(ApplicationInspectorService inspectorService, VcsService vcsService,
        ApplicationDbContext dbContext, ReportHandler reportHandler, DataSeeder dataSeeder,
        RepositoryService repositoryService)
    {
        _inspectorService = inspectorService;
        _vcsService = vcsService;
        _dbContext = dbContext;
        _reportHandler = reportHandler;
        _dataSeeder = dataSeeder;
        _repositoryService = repositoryService;
    }


    [HttpPost]
    public async Task<IActionResult> AnalyzeRepository(string repositoryUrl)
    {
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
        return View(new RepositoryCreateViewModel());
    }

    [HttpPost]
    public async Task<IActionResult> Create(RepositoryCreateViewModel model)
    {
        if (ModelState.IsValid)
        {
            // Check for existing repository by URL or name 
            var nameExists = _dbContext.RepositoryAnalysis.Any(ra => ra.Name == model.Name);
            if (nameExists)
            {
                ModelState.AddModelError("", "This repository name already exists.");
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
                    return View(model);
                }
            }


            var repositoryDownloadPath =
                _repositoryService.GetDynamicRepositoryStoragePath(model.Name, model.Branch ?? "master");


            if (!Directory.Exists(repositoryDownloadPath)) Directory.CreateDirectory(repositoryDownloadPath);


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
            
            if (model.ExcludedControllers != null && model.ExcludedControllers != null)
                newRepositoryAnalysis.ExcludedControllers = model.ExcludedControllers;
            
            if (model.ExcludedMethods != null && model.ExcludedMethods != null)
                newRepositoryAnalysis.ExcludedEndpoints = model.ExcludedMethods;

            await _vcsService.DownloadRepositoryAsync(newRepositoryAnalysis);
            newRepositoryAnalysis = await _inspectorService.AnalyzeRepositoryAsync(newRepositoryAnalysis);
            newRepositoryAnalysis = await _inspectorService.AnalyzeRepositoryForEndpointsAsync(newRepositoryAnalysis);
            await ApplicationInspectorService.GenerateReportAsync(newRepositoryAnalysis);
            await _reportHandler.ModifyAndSaveReport(newRepositoryAnalysis);
            await _repositoryService.CreateExcelFromRepositoryAsync(newRepositoryAnalysis);

            _dbContext.RepositoryAnalysis.Add(newRepositoryAnalysis);

            await _dbContext.SaveChangesAsync();

            if (model.IntegrateEndpoints) await _dataSeeder.SeedDataFromRepository(newRepositoryAnalysis);
            return RedirectToAction("Index");
        }

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
        _dbContext.RemoveRange(serviceStatuses);
        _dbContext.RemoveRange(apiEndpoints);
        _dbContext.RemoveRange(apiGroups);
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