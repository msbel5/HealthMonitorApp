using CurlGenerator.Core;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using HealthMonitorApp.Data;
using HealthMonitorApp.Models;

namespace HealthMonitorApp.Services
{
    public class CurlGeneratorService
    {
        private readonly ApplicationDbContext _context;

        public CurlGeneratorService(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<GeneratorResult> GenerateCurlScripts(string openApiJsonContent, string authorizationHeader, string baseUrl)
        {
            var openApiJsonFilePath = Path.Combine(Path.GetTempPath(), "openApiJsonFile.json");
            await File.WriteAllTextAsync(openApiJsonFilePath, openApiJsonContent);
            
            var settings = new GeneratorSettings
            {
                OpenApiPath = openApiJsonFilePath,
                AuthorizationHeader = authorizationHeader,
                BaseUrl = baseUrl,
                GenerateBashScripts = true
            };

            var scriptPath = Path.Combine(Path.GetTempPath(), "bashScripts");
            Directory.CreateDirectory(scriptPath);
            var result = await ScriptFileGenerator.Generate(settings);
            return result;
        }
    }
}