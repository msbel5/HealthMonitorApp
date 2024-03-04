using HealthMonitorApp.Data;
using Microsoft.AspNetCore.Mvc;
using HealthMonitorApp.Models;
using HealthMonitorApp.Services;
using HealthMonitorApp.Tools;
using HealthMonitorApp.ViewModels;
using Microsoft.EntityFrameworkCore;

namespace HealthMonitorApp.Controllers;

public class AnalysisController : Controller
{
    private readonly ApplicationInspectorService _inspectorService;
    private readonly VcsService _vcsService;
    private readonly ApplicationDbContext _dbContext;
    private readonly ReportHandler _reportHandler;
    private readonly DataSeeder _dataSeeder;
    private const string RepoPath = "Repos";



    public AnalysisController(ApplicationInspectorService inspectorService, VcsService vcsService, ApplicationDbContext dbContext, ReportHandler reportHandler, DataSeeder dataSeeder)
    {
        _inspectorService = inspectorService;
        _vcsService = vcsService;
        _dbContext = dbContext;
        _reportHandler = reportHandler;
        _dataSeeder = dataSeeder;
    }



    [HttpPost]
    public async Task<IActionResult> AnalyzeRepository(string repositoryUrl)
    {
        // Assume a model or method to fetch or create a RepositoryAnalysis object from the URL
        var repositoryAnalysis = await _inspectorService.AnalyzeRepositoryAsync(new RepositoryAnalysis { Url = repositoryUrl });
        
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
            bool nameExists = _dbContext.RepositoryAnalyses.Any(ra => ra.Name == model.Name);
            if (nameExists)
            {
                ModelState.AddModelError("", "This repository name already exists.");
                return View(model);
            }
            bool urlExists = _dbContext.RepositoryAnalyses.Any(ra => ra.Url == model.Url);
            if (urlExists)
            {
                // Optionally, check if a different branch for the same URL is being added
                bool branchExists = _dbContext.RepositoryAnalyses.Any(ra => ra.Url == model.Url && ra.Branch == model.Branch);
                if (branchExists)
                {
                    ModelState.AddModelError("", "This repository and branch combination already exists.");
                    return View(model);
                }
            }

            // If URL is unique or branch is different, proceed with repository creation
            var parentDirectory = Directory.GetParent(Environment.CurrentDirectory)?.Parent?.FullName;
            var repositoryDownloadPath = Path.Combine(parentDirectory, RepoPath, model.Name);

            var newRepositoryAnalysis = new RepositoryAnalysis
            {
                Name = model.Name,
                Url = model.Url,
                Branch = model.Branch ?? "master", // Default to 'master' if not specified
                Path = repositoryDownloadPath
            };

            if (model.Username != null && model.Password != null)
            {
                newRepositoryAnalysis.EncryptCredentials(model.Username, model.Password);
            }

            await _vcsService.DownloadRepositoryAsync(newRepositoryAnalysis);
            newRepositoryAnalysis = await _inspectorService.AnalyzeRepositoryAsync(newRepositoryAnalysis);
            newRepositoryAnalysis = await _inspectorService.AnalyzeRepositoryForEndpointsAsync(newRepositoryAnalysis);
            await _inspectorService.GenerateReportAsync(newRepositoryAnalysis);
            await _reportHandler.ModifyAndSaveReport(newRepositoryAnalysis);

            _dbContext.RepositoryAnalyses.Add(newRepositoryAnalysis);

            await _dbContext.SaveChangesAsync();

            if (model.IntegrateEndpoints == true)
            {
                await _dataSeeder.SeedDataFromRepository(newRepositoryAnalysis);
            }
            
            return RedirectToAction("Index");
        }

        // If model state is not valid, show the form again with validation messages
        return View(model);
    }


    
    public IActionResult Index()
    {
        // Fetch all repositories from the database
        var repositories = _dbContext.RepositoryAnalyses.ToList();
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
        var repository = _dbContext.RepositoryAnalyses.Find(id);
        return View(repository);
    }

    // Displays the form for editing a repository's details
    public IActionResult Edit(int id)
    {
        // Fetch repository by id for editing
        var repository = _dbContext.RepositoryAnalyses.Find(id); // Replace with actual fetch logic
        return View(repository);
    }

    [HttpPost]
    public IActionResult Edit(RepositoryAnalysis model)
    {
        if (ModelState.IsValid)
        {
            // Add logic to update the repository details
            return RedirectToAction("Index");
        }
        return View(model);
    }

    // Displays confirmation for deleting a repository
    public async Task<IActionResult> Delete(int id)
    {
        var repositoryAnalysis = await _dbContext.RepositoryAnalyses
            .FirstOrDefaultAsync(g => g.Id == id);

        if (repositoryAnalysis == null)
        {
            return NotFound();
        }

        if (repositoryAnalysis.ApiGroups.Count > 0) // Check if there are associated endpoints
        {
            // Return a view with an error message, or however you want to inform the user
            TempData["ErrorMessage"] = "Can't delete this Repository because it has associated Endpoints. Please delete or reassign those Endpoints first.";
            return RedirectToAction(nameof(Index));// Return the same view with an error message
        }

        return PartialView("_DeleteConfirmation", repositoryAnalysis);
    }

    [HttpPost, ActionName("Delete")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteConfirmed(int id)
    {
        var repositoryAnalysis = await _dbContext.RepositoryAnalyses.FindAsync(id);
        if (repositoryAnalysis == null)
        {
            return NotFound();
        }

        _dbContext.RepositoryAnalyses.Remove(repositoryAnalysis);
        await _vcsService.DeleteRepositoryAsync(repositoryAnalysis);
        await _dbContext.SaveChangesAsync();
        return RedirectToAction(nameof(Index));
    }
    
    
    public IActionResult GetReport(int id)
    {
        var repositoryAnalysis = _dbContext.RepositoryAnalyses.FirstOrDefault(ra => ra.Id == id);
        if (repositoryAnalysis == null)
        {
            return NotFound();
        }

        var reportPath = repositoryAnalysis.GetReportPath(); // Ensure this returns the path to your HTML report
        var content = System.IO.File.ReadAllText(reportPath);
        return Content(content, "text/html");
    }
}