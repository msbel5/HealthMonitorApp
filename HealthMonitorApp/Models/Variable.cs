using System.Text;

namespace HealthMonitorApp.Models;

public class Variable
{
    public Guid Id { get; set; }
    
    public string Name { get; set; }
    
    public string Value { get; set; }
    
    public ICollection<ApiEndpointVariable> ApiEndpointVariables { get; set; } = new List<ApiEndpointVariable>();
    public ICollection<ApiGroupVariable> ApiGroupVariables { get; set; } = new List<ApiGroupVariable>();
    public ICollection<RepositoryAnalysisVariable> RepositoryAnalysisVariables { get; set; } = new List<RepositoryAnalysisVariable>();
    
    public static string EncryptVariable(string Value)
    {
        // Placeholder for encryption logic
        return Encrypt(Value);
    }

    public  string DecryptVariable()
    {
        var variables = Decrypt(Value);
        return variables;
    }

    private static string Encrypt(string input)
    {
        // Implement encryption logic here
        return Convert.ToBase64String(Encoding.UTF8.GetBytes(input));
    }

    private static string Decrypt(string encryptedInput)
    {
        // Implement decryption logic here
        return Encoding.UTF8.GetString(Convert.FromBase64String(encryptedInput));
    }
}