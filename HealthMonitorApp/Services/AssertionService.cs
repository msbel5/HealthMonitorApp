using System.Net;
using HealthMonitorApp.Data;
using HealthMonitorApp.Models;
using Microsoft.CodeAnalysis.CSharp.Scripting;
using Microsoft.CodeAnalysis.Scripting;
using Tesseract;

namespace HealthMonitorApp.Services;

public class AssertionService
{
    private readonly ILogger<AssertionService> _logger;
    private const string UserScriptLog = "User Script: \n";
    private const string ScriptResultLog = "Script Execution Result: ";
    private const string ScriptCompilationErrorLog = "Script compilation error: ";
    private const string ScriptExecutionErrorLog = "Script execution error: ";

    public AssertionService( ILogger<AssertionService> logger)
    {
        _logger = logger;
    }

    public async Task<bool> ExecuteCustomAssertion(HttpResponseMessage response, string userScript)
    {
        try
        {
            userScript = DecodeUserScript(userScript);
            _logger.LogInformation(UserScriptLog + userScript);
            var scriptOptions = GetScriptOptions();
            var globals = new Globals { response = response };
            bool result = await CSharpScript.EvaluateAsync<bool>(userScript, scriptOptions, globals);
            _logger.LogInformation(ScriptResultLog + result);

            return result;
        }
        catch (CompilationErrorException e)
        {
            _logger.LogInformation(ScriptCompilationErrorLog + e.Message);
            return false;
        }
        catch (Exception e)
        {
            _logger.LogInformation(ScriptExecutionErrorLog + e.Message);
            return false;
        }
    }

    public ScriptOptions GetScriptOptions()
    {
        var scriptOptions = ScriptOptions.Default;
        AddReferences(scriptOptions);
        AddImports(scriptOptions);

        return scriptOptions;
    }

    private void AddReferences(ScriptOptions options)
    {
        options.AddReferences(
            typeof(HttpResponseMessage).Assembly,
            typeof(Newtonsoft.Json.JsonConvert).Assembly,
            typeof(Image).Assembly,
            typeof(iText.Kernel.Pdf.PdfDocument).Assembly,
            typeof(HtmlAgilityPack.HtmlDocument).Assembly,
            typeof(TesseractEngine).Assembly,
            typeof(Emgu.CV.Image<,>).Assembly  
        );
    }

    private void AddImports(ScriptOptions options)
    {
        options.AddImports(
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
    }

    public class Globals
    {
        public HttpResponseMessage response { get; set; }
    }
    
    public async Task<(bool Success, IEnumerable<string> Errors)> CheckCompilation(HttpResponseMessage response, string userScript)
    {
        try
        {
            userScript = DecodeUserScript(userScript);

            var scriptOptions = GetScriptOptions();
            var globals = new Globals { response = response };

            // Try compiling the script without executing it
            var script = CSharpScript.Create<bool>(userScript, scriptOptions, typeof(Globals));
            var diagnostics = script.Compile();
            
            if (diagnostics.Any(diagnostic => diagnostic.Severity == Microsoft.CodeAnalysis.DiagnosticSeverity.Error))
            {
                var errors = diagnostics.Where(diag => diag.Severity == Microsoft.CodeAnalysis.DiagnosticSeverity.Error)
                    .Select(diag => diag.ToString());
                return (false, errors);
            }

            return (true, Enumerable.Empty<string>());
        }
        catch (CompilationErrorException e)
        {
            return HandleException("Script compilation error: ", e);
        }
        catch (Exception e)
        {
            return HandleException("Error in checking script compilation: ", e);
        }
    }

    private string DecodeUserScript(string userScript)
    {
        return WebUtility.HtmlDecode(userScript);
    }

    private (bool, string[]) HandleException(string message, Exception e)
    {
        _logger.LogInformation(message + e.Message);
        return (false, new[] { e.Message });
    }

}