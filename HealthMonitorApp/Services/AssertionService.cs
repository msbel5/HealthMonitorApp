using HealthMonitorApp.Models;
using Microsoft.CodeAnalysis.CSharp.Scripting;
using Microsoft.CodeAnalysis.Scripting;

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
            _logger.LogInformation("User Script: \n" + userScript);

            var scriptOptions = ScriptOptions.Default
                .AddReferences(typeof(HttpResponseMessage).Assembly)
                .AddImports("System.Net.Http");

            var globals = new Globals { response = response };

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

    public class Globals
    {
        public HttpResponseMessage response { get; set; }
    }


    
    /*public async Task<bool> ExecuteCustomAssertion(HttpResponseMessage response, string userScript)
    {
        try
        {
            _logger.LogInformation("User Script: \n" + userScript);

            var scriptOptions = ScriptOptions.Default
                .AddReferences(typeof(HttpResponseMessage).Assembly)
                .AddImports("System.Net.Http");

            var completeScript = $@"
            bool AssertionMethod(HttpResponseMessage response)
            {{
                {userScript}
            }}
            return AssertionMethod(response);
        ";

            _logger.LogInformation("Complete Script: \n" + completeScript);

            var script = CSharpScript.Create<bool>(
                code: completeScript,
                options: scriptOptions);

            // Compile the script and log any compilation diagnostics
            var diagnostics = script.Compile();
            foreach (var diag in diagnostics)
            {
                _logger.LogInformation("Compilation Diagnostic: " + diag);
            }

            var delegateScript = script.CreateDelegate();

            // Execute the script
            bool result = await delegateScript(response);

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
    }*/
}