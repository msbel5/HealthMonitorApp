using System.Text;

namespace HealthMonitorApp.Models;

public class RepositoryAnalysis
{
    private const string Report = "report.html";


    public int Id { get; set; }

    public string Name { get; set; }
    public string Url { get; set; }

    public string? BaseUrl { get; set; }

    public string Branch { get; set; } = "master";

    public string? EncryptedUsername { get; set; }
    public string? EncryptedPassword { get; set; }
    public string Path { get; set; }
    public int NumberOfControllers { get; set; } = 0; // Default value
    public int NumberOfEndpoints { get; set; } = 0; // Default value
    public int NumberOfPublicEndpoints { get; set; } = 0; // Default value

    public string LatestCommitHash { get; set; } = string.Empty; // Default value

    public string? EncryptedVariables { get; set; } // JSON

    public ICollection<ApiGroup> ApiGroups { get; set; } = new List<ApiGroup>();


    // You might want methods to encrypt/decrypt the username and password for storage
    // Consider using a more secure method for encryption/decryption in a real application
    public void EncryptCredentials(string username, string password)
    {
        // Placeholder for encryption logic
        EncryptedUsername = Encrypt(username);
        EncryptedPassword = Encrypt(password);
    }

    public (string Username, string Password) DecryptCredentials()
    {
        // Placeholder for decryption logic
        var username = Decrypt(EncryptedUsername);
        var password = Decrypt(EncryptedPassword);
        return (username, password);
    }

    public void EncryptVariables(string Variables)
    {
        // Placeholder for encryption logic
        EncryptedUsername = Encrypt(Variables);
    }

    public string DecryptVariables()
    {
        var variables = Decrypt(EncryptedUsername);
        return variables;
    }

    private string Encrypt(string input)
    {
        // Implement encryption logic here
        return Convert.ToBase64String(Encoding.UTF8.GetBytes(input));
    }

    private string Decrypt(string encryptedInput)
    {
        // Implement decryption logic here
        return Encoding.UTF8.GetString(Convert.FromBase64String(encryptedInput));
    }

    public string GetReportPath()
    {
        return Path + "/" + Report;
    }
}