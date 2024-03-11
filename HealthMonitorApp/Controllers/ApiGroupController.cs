using HealthMonitorApp.Data;
using HealthMonitorApp.Models;
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
        return View(_context.ApiGroups.ToList());
    }

    // GET: Create new ApiGroup
    public IActionResult Create()
    {
        return View();
    }


    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create([Bind("Id,Name")] ApiGroup apiGroup)
    {
        if (ModelState.IsValid)
            try
            {
                _context.Add(apiGroup);
                await _context.SaveChangesAsync();
                return RedirectToAction(nameof(Index));
            }
            catch (Exception ex)
            {
                // Log the exception (ex) here or show an error message to the user
                ModelState.AddModelError("", "An error occurred while creating the API Group.");
            }

        // If we got this far, something failed, redisplay form
        return View(apiGroup);
    }


    // GET: Edit ApiGroup

    public async Task<IActionResult> Edit(Guid id)
    {
        var apiGroup = await _context.ApiGroups.FindAsync(id);
        if (apiGroup == null) return NotFound();
        return View(apiGroup);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(Guid id, [Bind("Id,Name")] ApiGroup apiGroup)
    {
        if (id != apiGroup.Id) return NotFound();

        if (ModelState.IsValid)
        {
            try
            {
                _context.Update(apiGroup);
                await _context.SaveChangesAsync();
            }
            catch (DbUpdateConcurrencyException)
            {
                if (!ApiGroupExists(apiGroup.Id))
                    return NotFound();
                throw;
            }

            return RedirectToAction(nameof(Index));
        }

        return View(apiGroup);
    }

    private bool ApiGroupExists(Guid id)
    {
        return _context.ApiGroups.Any(e => e.Id == id);
    }


    public async Task<IActionResult> Details(Guid id)
    {
        var apiGroup = await _context.ApiGroups.FindAsync(id);
        if (apiGroup == null) return NotFound();
        return View(apiGroup);
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
}