using HealthMonitorApp.Dtos;
using HealthMonitorApp.Services;
using Microsoft.AspNetCore.Mvc;

namespace HealthMonitorApp.Controllers;

public class ScriptController : Controller
{
    private readonly CurlGeneratorService _curlGeneratorService;
    private readonly ILogger<ScriptController> _logger;
    private readonly CurlCommandService _curlCommandService;


    public ScriptController(CurlGeneratorService curlGeneratorService, ILogger<ScriptController> logger, CurlCommandService curlCommandService)
    {
        _curlGeneratorService = curlGeneratorService;
        _logger = logger;
        _curlCommandService = curlCommandService;
    }

    [HttpGet]
    public IActionResult Index()
    {
        return View();
    }

    [HttpPost]
    public async Task<IActionResult> UploadOpenApiJson(IFormFile file, [FromForm] string authorizationHeader,
        [FromForm] string baseUrl)
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

    
    
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SaveCurlCommand([FromBody] CurlCommandDto curlCommandDto)
    {
        if (curlCommandDto == null || string.IsNullOrWhiteSpace(curlCommandDto.Content))
        {
            return BadRequest("Invalid cURL command.");
        }

        try
        {
            await _curlCommandService.SaveCurlCommandDto(curlCommandDto);
            return Ok("cURL command saved successfully.");
        }
        catch (Exception ex)
        {
            _logger.LogError($"Error saving cURL command: {ex.Message}");
            return StatusCode(500, "An error occurred while saving the cURL command.");
        }
    }

    [HttpPost("saveAll")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SaveAllCurlCommands([FromBody] SaveAllCurlCommandsDto saveAllCurlCommandsDto)
    {
        if (saveAllCurlCommandsDto == null || saveAllCurlCommandsDto.Files == null || !saveAllCurlCommandsDto.Files.Any())
        {
            return BadRequest("No files provided.");
        }

        try
        {
            await _curlCommandService.SaveAllCurlCommandDtos(saveAllCurlCommandsDto.Files);
            return Ok("All cURL commands saved successfully.");
        }
        catch (Exception ex)
        {
            _logger.LogError($"Error saving all cURL commands: {ex.Message}");
            return StatusCode(500, "An error occurred while saving the cURL commands.");
        }
    }
}