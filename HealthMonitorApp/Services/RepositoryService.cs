using HealthMonitorApp.Data;
using HealthMonitorApp.Models;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json;

namespace HealthMonitorApp.Services;

public class RepositoryService
{
    private ApplicationDbContext _dbContext;
    
    public RepositoryService(ApplicationDbContext dbContext)
    {
        _dbContext = dbContext;
    }
    
    
    public async Task<RepositoryAnalysis?> GetRepositoryAnalysisByUrlAsync(string repositoryUrl)
    {
        return await _dbContext.RepositoryAnalyses.FirstOrDefaultAsync(ra => ra.Url == repositoryUrl);
    }
    
    public async Task<RepositoryAnalysis?> GetRepositoryAnalysisByNameAsync(string repositoryName)
    {
        return await _dbContext.RepositoryAnalyses.FirstOrDefaultAsync(ra => ra.Name == repositoryName);
    }
    

    public async Task<List<RepositoryAnalysis?>> GetAllRepositoryAnalysis()
    {
        return await _dbContext.RepositoryAnalyses.ToListAsync();
    }
    
    public async Task<RepositoryAnalysis?> GetRepositoryAnalysisByIdAsync(int id)
    {
        return await _dbContext.RepositoryAnalyses.FindAsync(id);
    }
    
    public async Task SaveRepositoryAnalysisAsync(RepositoryAnalysis repositoryAnalysis)
    {
        _dbContext.RepositoryAnalyses.Add(repositoryAnalysis);
        await _dbContext.SaveChangesAsync();
    }
    
    public async Task UpdateRepositoryAnalysisAsync(RepositoryAnalysis repositoryAnalysis)
    {
        _dbContext.RepositoryAnalyses.Update(repositoryAnalysis);
        await _dbContext.SaveChangesAsync();
    }
    
    public async Task DeleteRepositoryAnalysisAsync(RepositoryAnalysis repositoryAnalysis)
    {
        _dbContext.RepositoryAnalyses.Remove(repositoryAnalysis);
        await _dbContext.SaveChangesAsync();
    }
    
    // get api mapping from repository
    public async Task<string> GetApiMappingByRepositoryIdAsync(RepositoryAnalysis repositoryAnalysis)
    {
        return "api mapping";
    }
    
    
    public async Task<string> ExtractControllersAndEndpointsAsJsonAsync(RepositoryAnalysis? repositoryAnalysis)
    {
        var apiGroups = new List<ApiGroup>();
        var allFiles = Directory.GetFiles(repositoryAnalysis.Path, "*.cs", SearchOption.AllDirectories);
        var compilation = await CreateCompilationAsync(allFiles);
        
        foreach (var tree in compilation.SyntaxTrees)
        {
            var semanticModel = compilation.GetSemanticModel(tree);
            var root = tree.GetRoot();

            var controllers = root.DescendantNodes().OfType<ClassDeclarationSyntax>()
                .Where(node => node.BaseList?.Types.Any(type => type.ToString().Contains("Controller")) ?? false);

            foreach (var controller in controllers)
            {
                var controllerAttributes = controller.AttributeLists.SelectMany(attrList => attrList.Attributes);
                string controllerAnnotationsListed = string.Join(", ", controllerAttributes);
                var isControllerAuthorized = controllerAttributes.Any(attr => attr.Name.ToString().Contains("Authorize"));
            
                var apiGroup = new ApiGroup()
                {
                    Name = controller.Identifier.Text,
                    IsAuthorized = isControllerAuthorized,
                    Annotations = controllerAnnotationsListed
                };

                foreach (var method in controller.Members.OfType<MethodDeclarationSyntax>())
                {
                    var methodAttributes = method.AttributeLists.SelectMany(attrList => attrList.Attributes);
                    string methodAnnotationsListed = string.Join(", ", methodAttributes);
                    var isMethodAuthorized = methodAttributes.Any(attr => attr.Name.ToString().Contains("Authorize")) || isControllerAuthorized;
                    var isMethodOpen = methodAttributes.Any(attr => attr.Name.ToString().Contains("AllowAnonymous"));

                    var apiEndPoint = new ApiEndpoint()
                    {
                        Name = method.Identifier.Text,
                        IsAuthorized = isMethodAuthorized,
                        IsOpen = !isMethodAuthorized || isMethodOpen,
                        Annotations = methodAnnotationsListed
                    };

                    apiGroup.ApiEndpoints.Add(apiEndPoint);
                }

                apiGroups.Add(apiGroup);
            }
        }

        var json = JsonConvert.SerializeObject(apiGroups, Formatting.Indented);
        return json;
    }

    private async Task<Compilation> CreateCompilationAsync(string[] filePaths)
    {
        var syntaxTrees = new List<SyntaxTree>();
        var options = CSharpParseOptions.Default.WithLanguageVersion(LanguageVersion.CSharp9);

        foreach (var path in filePaths)
        {
            var code = await File.ReadAllTextAsync(path);
            syntaxTrees.Add(CSharpSyntaxTree.ParseText(code, options));
        }

        var references = new List<MetadataReference>
        {
            MetadataReference.CreateFromFile(typeof(object).Assembly.Location)
            // Add other necessary references
        };

        return CSharpCompilation.Create("Analysis", syntaxTrees, references);
    }
    
    public async Task<string> GetApiMappingFromRepositoryAsync(RepositoryAnalysis repositoryAnalysis)
    {
       return "api mapping"; 
       
    }
    
}