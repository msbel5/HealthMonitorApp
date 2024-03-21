using System.Text.RegularExpressions;
using HealthMonitorApp.Data;
using HealthMonitorApp.Models;
using Jint;
using Microsoft.EntityFrameworkCore;

namespace HealthMonitorApp.Services;

public class DynamicStringProcessor(ApplicationDbContext context)
{
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
        return curlCommand;
    }

    private string ApplyVariables(ApiEndpoint apiEndpoint)
    {
        
        var rootApiGroup =  context.ApiGroups
            .FirstOrDefault(ag => apiEndpoint != null && ag.Id == apiEndpoint.ApiGroupId);
        
        var repositoryAnalysis = context.RepositoryAnalysis
            .FirstOrDefault(ra => rootApiGroup != null && ra.Id == rootApiGroup.RepositoryAnalysisId);
        
        var repositoryAnalysisVariables = context.RepositoryAnalysisVariables
            .Where(rlv => repositoryAnalysis != null && rlv.RepositoryAnalysisId == repositoryAnalysis.Id)
            .Include(repositoryAnalysisVariable => repositoryAnalysisVariable.Variable).ToList();
        
        var apiGroupVariables = context.ApiGroupVariables
            .Where(agv => rootApiGroup != null && agv.ApiGroupId == rootApiGroup.Id)
            .Include(apiGroupVariable => apiGroupVariable.Variable).ToList();
        
        var apiEndpointVariables = context.ApiEndpointVariables.Where(aev => apiEndpoint != null && aev.ApiEndpointId == apiEndpoint.Id)
            .Include(apiEndpointVariable => apiEndpointVariable.Variable).ToList();
        
        
        var curlCommand = apiEndpoint?.cURL;
        var variablePattern = @"\$\[\[(.*?)\]\]"; // regex pattern to extract variable name $[[]]
        if (curlCommand != null)
        {
            var matches = Regex.Matches(curlCommand, variablePattern);

            foreach (Match match in matches)
            {
                var variableName = match.Groups[1].Value; // extract variable name without $[[]]
                var variableValue =  repositoryAnalysisVariables.FirstOrDefault(rlv => rlv.Variable.Name == (variableName))?.Variable.DecryptVariable() ??
                                     apiGroupVariables.FirstOrDefault(agv => agv.Variable.Name == variableName)?.Variable.DecryptVariable() ??
                                     apiEndpointVariables.FirstOrDefault(aev => aev.Variable.Name == variableName)?.Variable.DecryptVariable();
                curlCommand = curlCommand.Replace(match.Value, variableValue); // replace variable with the value in the original string
            }
        }

        return curlCommand ?? "variableFaulty";
    }


    public bool ValidateJavaScript(ServiceStatus serviceStatus)
    {
        if (string.IsNullOrEmpty(serviceStatus.AssertionScript))
            return true;
        var script = serviceStatus.AssertionScript;
        // Disallow these methods/objects
        var disallowed = new[] { "eval", "Function", "window", "document", "require" };

        foreach (var item in disallowed)
            if (script.Contains(item))
                return false;

        return true;
    }
}