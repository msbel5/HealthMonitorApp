using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using System.IO;
using System.Threading.Tasks;
using HealthMonitorApp.Services;
using CurlGenerator.Core;

public class ScriptController : Controller
{
    private readonly CurlGeneratorService _curlGeneratorService;

    public ScriptController(CurlGeneratorService curlGeneratorService)
    {
        _curlGeneratorService = curlGeneratorService;
    }

    [HttpGet]
    public IActionResult Index()
    {
        return View();
    }

    [HttpPost]
    public async Task<IActionResult> UploadOpenApiJson(IFormFile file, [FromForm] string authorizationHeader, [FromForm] string baseUrl)
    {
        if (file == null || file.Length == 0)
        {
            ViewBag.Error = "File is not selected";
            return View("Index");
        }

        string openApiJson;
        using (var streamReader = new StreamReader(file.OpenReadStream()))
        {
            openApiJson = await streamReader.ReadToEndAsync();
        }

        var result = await _curlGeneratorService.GenerateCurlScripts(openApiJson, authorizationHeader, baseUrl);
        return View("Result", result);
    }
}