namespace HealthMonitorApp.Models;

public class Settings
{
    public Guid Id { get; set; }
    public TimeSpan TestInterval { get; set; }
    public string NotificationEmails { get; set; }
    public string SmtpServer { get; set; }
    public int SmtpPort { get; set; }
    public string SmtpUsername { get; set; }
    public string SmtpPassword { get; set; }
}