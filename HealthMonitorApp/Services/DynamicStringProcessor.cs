using System.Text.RegularExpressions;
using HealthMonitorApp.Data;
using HealthMonitorApp.Models;
using Jint;
using Microsoft.EntityFrameworkCore;

namespace HealthMonitorApp.Services;

public class DynamicStringProcessor(ApplicationDbContext context) {
    private readonly Logger<DynamicStringProcessor> _logger = new Logger<DynamicStringProcessor>(new LoggerFactory());
    public string Process(ApiEndpoint apiEndpoint)
    {
        var curlCommand = ApplyVariables(apiEndpoint);
        var jsPattern = @"\$\{\{(.*?)\}\}"; // regex pattern to extract variables code
        var matches = Regex.Matches(curlCommand, jsPattern);

        foreach (Match match in matches)
        {
            var jsCode = match.Groups[1].Value; // extract JS code without ${{}}
            var engine = new Engine();
            var result = engine.Execute(jsCode).GetCompletionValue().ToString(); // execute JS code
            curlCommand =
                curlCommand.Replace(match.Value, result); // replace JS code with the result in the original string
        }
        _logger.LogInformation("Processed cURL command: " + curlCommand);
        return curlCommand;
    }

    private string ApplyVariables(ApiEndpoint apiEndpoint)
    {
        
        var rootApiGroup =  context.ApiGroups
            .FirstOrDefault(ag => ag.Id == apiEndpoint.ApiGroupId);
        
        var repositoryAnalysis = context.RepositoryAnalysis
            .FirstOrDefault(ra => rootApiGroup != null && ra.Id == rootApiGroup.RepositoryAnalysisId);
        
        var repositoryAnalysisVariables = context.Variables
            .Where(rlv => repositoryAnalysis != null && rlv.RepositoryAnalysisId == repositoryAnalysis.Id)
            .Include(rlv => rlv.RepositoryAnalysis) ;
        
        var apiGroupVariables = context.Variables
            .Where(agv => rootApiGroup != null && agv.ApiGroupId == rootApiGroup.Id)
            .Include(apiGroupVariable => apiGroupVariable.ApiGroup).ToList();
        
        
        var curlCommand = apiEndpoint?.cURL;
        var variablePattern = @"\$\[\[(.*?)\]\]"; // regex pattern to extract variable name $[[]]
        if (curlCommand != null)
        {
            var matches = Regex.Matches(curlCommand, variablePattern);

            foreach (Match match in matches)
            {
                var variableName = match.Groups[1].Value; // extract variable name without $[[]]
                var variableValue =  repositoryAnalysisVariables.FirstOrDefault(rlv => rlv.Name == (variableName))?.DecryptVariable() ??
                                     apiGroupVariables.FirstOrDefault(agv => agv.Name == variableName)?.DecryptVariable();
                curlCommand = curlCommand.Replace(match.Value, variableValue); // replace variable with the value in the original string
            }
        }
        

        _logger.LogInformation("Applied variables to cURL command: " + curlCommand);
        
        return curlCommand ?? "variableFaulty";
    }


    public bool ValidateJavaScript(ServiceStatus serviceStatus)
    {
        if (string.IsNullOrEmpty(serviceStatus.AssertionScript))
        {
            _logger.LogInformation("Script is empty");
            return true;
        }
        var script = serviceStatus.AssertionScript;
        // Disallow these methods/objects
        var disallowed = new[] { "eval", "Function", "window", "document", "require" };

        foreach (var item in disallowed)
            if (script.Contains(item))
            {
                _logger.LogInformation("Script contains disallowed method or object: " + item);
                return false;
            }
        _logger.LogInformation("Script is valid");
        return true;
    }
}