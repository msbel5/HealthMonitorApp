using HealthMonitorApp.Data;
using HealthMonitorApp.Models;
using HealthMonitorApp.Services;
using HealthMonitorApp.Tools;
using HealthMonitorApp.ViewModels;
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
                BaseUrl = model.BaseUrl,
                EncryptedVariables = model.Variables
            };

            if (model.Username != null && model.Password != null)
                newRepositoryAnalysis.EncryptCredentials(model.Username, model.Password);

            await _vcsService.DownloadRepositoryAsync(newRepositoryAnalysis);
            newRepositoryAnalysis = await _inspectorService.AnalyzeRepositoryAsync(newRepositoryAnalysis);
            newRepositoryAnalysis = await _inspectorService.AnalyzeRepositoryForEndpointsAsync(newRepositoryAnalysis);
            await ApplicationInspectorService.GenerateReportAsync(newRepositoryAnalysis);
            await _reportHandler.ModifyAndSaveReport(newRepositoryAnalysis);

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
    public IActionResult Details(int id)
    {
        var repository = _dbContext.RepositoryAnalysis.Find(id);
        return View(repository);
    }

    // Displays the form for editing a repository's details
    public IActionResult Edit(int id)
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
    public async Task<IActionResult> Delete(int id)
    {
        var repositoryAnalysis = await _dbContext.RepositoryAnalysis
            .FirstOrDefaultAsync(g => g.Id == id);

        if (repositoryAnalysis == null) return NotFound();

        if (repositoryAnalysis.ApiGroups.Count > 0) // Check if there are associated endpoints
        {
            // Return a view with an error message, or however you want to inform the user
            TempData["ErrorMessage"] =
                "Can't delete this Repository because it has associated Endpoints. Please delete or reassign those Endpoints first.";
            return RedirectToAction(nameof(Index)); // Return the same view with an error message
        }

        return PartialView("_DeleteConfirmation", repositoryAnalysis);
    }

    [HttpPost]
    [ActionName("Delete")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteConfirmed(int id)
    {
        var repositoryAnalysis = await _dbContext.RepositoryAnalysis.FindAsync(id);
        if (repositoryAnalysis == null) return NotFound();

        _dbContext.RepositoryAnalysis.Remove(repositoryAnalysis);
        await _vcsService.DeleteRepositoryAsync(repositoryAnalysis);
        await _dbContext.SaveChangesAsync();
        return RedirectToAction(nameof(Index));
    }


    public IActionResult GetReport(int id)
    {
        var repositoryAnalysis = _dbContext.RepositoryAnalysis.FirstOrDefault(ra => ra.Id == id);
        if (repositoryAnalysis == null) return NotFound();

        var reportPath = repositoryAnalysis.GetReportPath(); // Ensure this returns the path to your HTML report
        var content = System.IO.File.ReadAllText(reportPath);
        return Content(content, "text/html");
    }
}