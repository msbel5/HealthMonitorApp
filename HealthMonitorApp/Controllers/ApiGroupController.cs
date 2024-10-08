using HealthMonitorApp.Data;
using HealthMonitorApp.Models;
using HealthMonitorApp.ViewModels;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json.Linq;

namespace HealthMonitorApp.Controllers;

public class ApiGroupController : Controller
{
    private readonly ApplicationDbContext _context;

    public ApiGroupController(ApplicationDbContext context)
    {
        _context = context;
    }

    // GET: List of ApiGroups
    public IActionResult Index()
    {
        var apiGroups = _context.ApiGroups.Include(a => a.RepositoryAnalysis).ToList();

        return View(_context.ApiGroups.ToList());
    }

    // GET: Create new ApiGroup
    public IActionResult Create()
    {
        return View();
    }


    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create([Bind("ApiGroup, Variables")] ApiGroupViewModel apiGroupViewModel)
    {
        if (ModelState.IsValid)
            try
            {
                var apiGroup = apiGroupViewModel.ApiGroup;
                _context.Add(apiGroup); // Ensure you're adding the ApiGroup entity
                // Save the apiGroupViewModel to generate its ID if it's generated by the database
                await _context.SaveChangesAsync();
                if (apiGroupViewModel.Variables != null && apiGroupViewModel.Variables.Count > 0)
                {
                    foreach (var variableViewModel in apiGroupViewModel.Variables)
                    {
                        var encryptedValue =
                            Variable.EncryptVariable(variableViewModel
                                .Value); // Assuming EncryptVariable is a static method or accessible here
                        var apiGroupVariable = new Variable
                        {
                            ApiGroupId = apiGroup.Id, // Use the generated Id from the saved ApiGroup
                            Name = variableViewModel.Name,
                            Value = encryptedValue
                        };
                        _context.Variables.Add(
                            apiGroupVariable); // Assuming your DbContext has a DbSet for ApiGroupVariables
                    }

                    await _context.SaveChangesAsync();
                }

                return RedirectToAction(nameof(Index));
            }
            catch (Exception ex)
            {
                // Log the exception (ex) here or show an error message to the user
                ModelState.AddModelError("", "An error occurred while creating the API Group.");
            }

        // If we got this far, something failed, redisplay form
        return View(apiGroupViewModel);
    }

    // GET: Edit ApiGroup
    public async Task<IActionResult> Edit(Guid id)
    {
        var apiGroup = await _context.ApiGroups.FindAsync(id);
        if (apiGroup == null) return NotFound();
        var apiGroupVariables = await _context.Variables
            .Where(v => v.ApiGroupId == id)
            .ToListAsync();
        var variableIds = apiGroupVariables.Select(v => v.Id).ToList();
        var variables = _context.Variables.Where(v => variableIds.Contains(v.Id)).ToList();
        var apiGroupViewModel = new ApiGroupViewModel
        {
            ApiGroup = apiGroup,
            Variables = variables
        };
        return View(apiGroupViewModel);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(Guid id, [Bind("ApiGroup, Variables")] ApiGroupViewModel apiGroupViewModel)
    {
        if (id != apiGroupViewModel.ApiGroup.Id) return NotFound();
        if (ModelState.IsValid)
            try
            {
                _context.Update(apiGroupViewModel.ApiGroup); // Update the ApiGroup information
                // Retrieve existing variables for this ApiGroup
                var existingVariables = await _context.Variables
                    .Where(v => v.ApiGroupId == id).Include(apiGroupVariable => apiGroupVariable.ApiGroup)
                    .ToListAsync();
                // Handle removed variables
                foreach (var existingVariable in existingVariables)
                    if (!apiGroupViewModel.Variables.Any(v => v.Id == existingVariable.Id))
                        _context.Variables
                            .Remove(existingVariable); // Remove variables not present in the submitted list
                // Handle added or updated variables
                foreach (var variable in apiGroupViewModel.Variables)
                {
                    var existingVariable = existingVariables.FirstOrDefault(v => v.Id == variable.Id);
                    if (existingVariable != null)
                    {
                        // Update existing variable
                        existingVariable.Name = variable.Name;
                        existingVariable.Value = Variable.EncryptVariable(variable.Value);
                        _context.Update(existingVariable);
                    }
                    else
                    {
                        // Add new variable
                        var newVariable = new Variable
                        {
                            ApiGroupId = apiGroupViewModel.ApiGroup.Id,
                            Name = variable.Name,
                            Value = Variable.EncryptVariable(variable.Value)
                        };
                        _context.Variables.Add(newVariable);
                    }
                }

                await _context.SaveChangesAsync();
                return RedirectToAction(nameof(Index));
            }
            catch (DbUpdateConcurrencyException)
            {
                if (!ApiGroupExists(apiGroupViewModel.ApiGroup.Id))
                    return NotFound();
                throw;
            }
            catch (Exception ex)
            {
                // Log the exception or handle it as needed
                ModelState.AddModelError("", "An error occurred while updating the API Group.");
            }

        // If we got this far, something failed, redisplay form
        return View(apiGroupViewModel);
    }

    public async Task<IActionResult> Details(Guid id)
    {
        var apiGroup = await _context.ApiGroups.FindAsync(id);
        if (apiGroup == null) return NotFound();
        var apiGroupVariables = await _context.Variables
            .Where(v => v.ApiGroupId == id)
            .ToListAsync();
        var variableIds = apiGroupVariables.Select(v => v.Id).ToList();
        var variables = _context.Variables.Where(v => variableIds.Contains(v.Id)).ToList();
        var apiGroupViewModel = new ApiGroupViewModel
        {
            ApiGroup = apiGroup,
            Variables = variables
        };
        return View(apiGroupViewModel);
    }


    // GET: Delete ApiGroup
    public async Task<IActionResult> Delete(Guid id)
    {
        var apiGroup = await _context.ApiGroups
            .Include(g => g.ApiEndpoints)
            .ThenInclude(ae => ae.ServiceStatus)
            .FirstOrDefaultAsync(g => g.Id == id);

        if (apiGroup == null) return NotFound();
        return PartialView("_DeleteConfirmation", apiGroup);
    }

    [HttpPost]
    [ActionName("Delete")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteConfirmed(Guid id)
    {
        var apiGroup = await _context.ApiGroups
            .Include(g => g.ApiEndpoints)
            .ThenInclude(ae => ae.ServiceStatus)
            .FirstOrDefaultAsync(g => g.Id == id);
        if (apiGroup == null) return NotFound();
        foreach (var apiEndpoint in apiGroup.ApiEndpoints)
        {
            _context.ApiEndpoints.Remove(apiEndpoint);
            _context.ServiceStatuses.Remove(apiEndpoint.ServiceStatus);
        }

        var variables = _context.Variables.Where(v => v.ApiGroupId == id).ToList();
        _context.Variables.RemoveRange(variables);
        _context.ApiGroups.Remove(apiGroup);
        await _context.SaveChangesAsync();
        return RedirectToAction(nameof(Index));
    }


    [HttpPost]
    public async Task<IActionResult> FetchAuthToken(string authCurl)
    {
        using (var client = new HttpClient())
        {
            var response = await client.GetAsync(authCurl);
            if (response.IsSuccessStatusCode)
            {
                var responseData = await response.Content.ReadAsStringAsync();
                var jsonObject = JObject.Parse(responseData);
                var keys = jsonObject.Properties().Select(p => p.Name).ToList();
                return Json(new { success = true, keys });
            }

            return Json(new { success = false, message = "Failed to fetch token." });
        }
    }

    private bool ApiGroupExists(Guid id)
    {
        return _context.ApiGroups.Any(e => e.Id == id);
    }
}