using System.ComponentModel.DataAnnotations;

namespace HealthMonitorApp.ViewModels;

public class SettingsViewModel
{
    [Required]
    [Display(Name = "Test Interval (minutes)")]
    public int TestIntervalMinutes { get; set; }

    [Required]
    [Display(Name = "Notification Emails")]
    public string NotificationEmails { get; set; }

    [Required]
    [Display(Name = "SMTP Server")]
    public string SmtpServer { get; set; }

    [Required]
    [Display(Name = "SMTP Port")]
    public int SmtpPort { get; set; }

    [Required]
    [Display(Name = "SMTP Username")]
    public string SmtpUsername { get; set; }

    [Required]
    [Display(Name = "SMTP Password")]
    public string SmtpPassword { get; set; }
}