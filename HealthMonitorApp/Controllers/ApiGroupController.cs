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
    public async Task<IActionResult> Create([Bind("ID,Name")] ApiGroup apiGroup)
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

    public async Task<IActionResult> Edit(int id)
    {
        var apiGroup = await _context.ApiGroups.FindAsync(id);
        if (apiGroup == null) return NotFound();
        return View(apiGroup);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int id, [Bind("ID,Name")] ApiGroup apiGroup)
    {
        if (id != apiGroup.ID) return NotFound();

        if (ModelState.IsValid)
        {
            try
            {
                _context.Update(apiGroup);
                await _context.SaveChangesAsync();
            }
            catch (DbUpdateConcurrencyException)
            {
                if (!ApiGroupExists(apiGroup.ID))
                    return NotFound();
                throw;
            }

            return RedirectToAction(nameof(Index));
        }

        return View(apiGroup);
    }

    private bool ApiGroupExists(int id)
    {
        return _context.ApiGroups.Any(e => e.ID == id);
    }


    public async Task<IActionResult> Details(int id)
    {
        var apiGroup = await _context.ApiGroups.FindAsync(id);
        if (apiGroup == null) return NotFound();
        return View(apiGroup);
    }


    // GET: Delete ApiGroup
    public async Task<IActionResult> Delete(int id)
    {
        var apiGroup = await _context.ApiGroups
            .Include(g => g.ApiEndpoints) // Include related ApiEndpoints
            .FirstOrDefaultAsync(g => g.ID == id);

        if (apiGroup == null) return NotFound();

        if (apiGroup.ApiEndpoints.Count > 0) // Check if there are associated endpoints
        {
            // Return a view with an error message, or however you want to inform the user
            TempData["ErrorMessage"] =
                "Can't delete this API Group because it has associated Endpoints. Please delete or reassign those Endpoints first.";
            return RedirectToAction(nameof(Index)); // Return the same view with an error message
        }

        return PartialView("_DeleteConfirmation", apiGroup);
    }

    [HttpPost]
    [ActionName("Delete")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteConfirmed(int id)
    {
        var apiGroup = await _context.ApiGroups.FindAsync(id);
        if (apiGroup == null) return NotFound();

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