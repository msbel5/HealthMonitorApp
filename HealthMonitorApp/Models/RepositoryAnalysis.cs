namespace HealthMonitorApp.Models;

public class RepositoryAnalysis
{
    private const string Report = "report.html";
    
    public int Id { get; set; }
    
    public string Name { get; set; }
    public string Url { get; set; }
    
    public string? BaseUrl { get; set; }
    
    private string _branch = "master"; // Default branch set to 'master'
    public string Branch
    {
        get => _branch;
        set => _branch = value;
    }
    public string? EncryptedUsername { get; set; }
    public string? EncryptedPassword { get; set; }
    public string Path { get; set; }
    public int NumberOfControllers { get; set; } = 0; // Default value
    public int NumberOfEndpoints { get; set; } = 0; // Default value
    public int NumberOfPublicEndpoints { get; set; } = 0; // Default value

    public string LatestCommitHash { get; set; } = string.Empty; // Default value

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
        string username = Decrypt(EncryptedUsername);
        string password = Decrypt(EncryptedPassword);
        return (username, password);
    }

    private string Encrypt(string input)
    {
        // Implement encryption logic here
        return Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(input));
    }

    private string Decrypt(string encryptedInput)
    {
        // Implement decryption logic here
        return System.Text.Encoding.UTF8.GetString(Convert.FromBase64String(encryptedInput));
    }
    
    public string GetReportPath()
    {
        return Path+ "/" + Report;
    }
    
    
}