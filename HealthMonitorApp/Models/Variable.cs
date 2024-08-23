using System.Text;

namespace HealthMonitorApp.Models;

public class Variable
{
    private string _value;
    public Guid Id { get; set; }

    public string Name { get; set; }

    public string Value { get; set; }

    // References for hierarchy
    public Guid? ApiGroupId { get; set; }
    public ApiGroup? ApiGroup { get; set; }

    public Guid? RepositoryAnalysisId { get; set; }
    public RepositoryAnalysis? RepositoryAnalysis { get; set; }

    public static string EncryptVariable(string value)
    {
        // Placeholder for encryption logic
        var val = Encrypt(value);
        return val;
    }

    public string DecryptVariable()
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