using System.Net;
using HealthMonitorApp.Data;
using HealthMonitorApp.Models;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Scripting;
using Microsoft.CodeAnalysis.Scripting;
using Tesseract;

namespace HealthMonitorApp.Services;

public class AssertionService
{
    private readonly ApplicationDbContext _context;
    private readonly ILogger<AssertionService> _logger;


    public AssertionService(ApplicationDbContext context , ILogger<AssertionService> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task<bool> ExecuteCustomAssertion(HttpResponseMessage response, string userScript)
    {
        try
        {
            userScript = WebUtility.HtmlDecode(userScript);;
            
            _logger.LogInformation("User Script: \n" + userScript);

            var scriptOptions = GetScriptOptions();

            var globals = new ScriptGlobals { response = response };

            bool result = await CSharpScript.EvaluateAsync<bool>(
                userScript, scriptOptions, globals);

            _logger.LogInformation("Script Execution Result: " + result);
            return result;
        }
        catch (CompilationErrorException e)
        {
            _logger.LogInformation("Script compilation error: " + e.Message);
            return false;
        }
        catch (Exception e)
        {
            _logger.LogInformation("Script execution error: " + e.Message);
            return false;
        }
    }
    
    public ScriptOptions GetScriptOptions()
    {
        var scriptOptions = ScriptOptions.Default
            .AddReferences(
                // References to the assemblies
                typeof(HttpResponseMessage).Assembly,
                typeof(Newtonsoft.Json.JsonConvert).Assembly,
                typeof(SixLabors.ImageSharp.Image).Assembly,
                typeof(iText.Kernel.Pdf.PdfDocument).Assembly,
                typeof(HtmlAgilityPack.HtmlDocument).Assembly,
                typeof(Tesseract.TesseractEngine).Assembly, 
                typeof(Emgu.CV.Image<,>).Assembly 
            )
            .AddImports(
                // Pre-imported namespaces
                "System",
                "System.Net.Http",
                "System.IO",
                "System.Linq",
                "System.Text",
                "System.Collections.Generic",
                "Newtonsoft.Json",
                "SixLabors.ImageSharp",
                "iText.Kernel.Pdf",
                "HtmlAgilityPack",
                "Tesseract", 
                "Emgu.CV",
                "Emgu.CV.Structure"
            );

        return scriptOptions;
    }

    public class ScriptGlobals
    {
        public HttpResponseMessage response { get; set; }
    }
    
    public async Task<(bool isValid, List<string> diagnostics)> CheckScript(string userScript, HttpResponseMessage response)
    {
        var diagnostics = new List<string>();
        var isValid = false;

        try
        {
            // Check for script correctness without executing it
            var scriptOptions = GetScriptOptions();
            var script = CSharpScript.Create<bool>(userScript, scriptOptions, typeof(ScriptGlobals));
            var compilation = script.GetCompilation();
            var result = compilation.GetDiagnostics();

            foreach (var diagnostic in result)
            {
                if (diagnostic.Severity == DiagnosticSeverity.Error)
                {
                    diagnostics.Add(diagnostic.ToString());
                }
            }

            // If no errors, execute the script with HttpResponseMessage
            if (!diagnostics.Any())
            {
                var globals = new ScriptGlobals { response = response };
                bool executionResult = await script.RunAsync(globals).ContinueWith(t => t.Result.ReturnValue);
                isValid = true;
                _logger.LogInformation("Script Execution Result: " + executionResult);
            }
        }
        catch (Exception e)
        {
            diagnostics.Add("Error in script execution: " + e.Message);
        }

        return (isValid, diagnostics);
    }
    
}