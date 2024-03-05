using System.Text.RegularExpressions;
using Jint;

namespace HealthMonitorApp.Services;

public class DynamicStringProcessor
{
    public string Process(string curlCommand)
    {
        var jsPattern = @"\$\{\{(.*?)\}\}"; // regex pattern to extract JS code
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

    public bool ValidateScript(string script)
    {
        // Disallow these methods/objects
        var disallowed = new[] { "eval", "Function", "window", "document", "require" };

        foreach (var item in disallowed)
            if (script.Contains(item))
                return false;

        return true;
    }
}