using System.ComponentModel.DataAnnotations;
using HealthMonitorApp.Models;

namespace HealthMonitorApp.ViewModels;

public class ServiceStatusCreateViewModel
{
    [Required]
    [StringLength(100, ErrorMessage = "Service name must be under 100 characters.")]
    public string ServiceName { get; set; }

    [Required]
    [Range(100, 599, ErrorMessage = "Expected code must be a valid HTTP status code.")]
    public int ExpectedStatusCode { get; set; }

    [Required]
    [RegularExpression(@"^(https?:\/\/.+|curl\s+.+)$",
        ErrorMessage = "The cURL field must be a valid URL or cURL command.")]
    public string cURL { get; set; }

    public string? AssertionScript { get; set; }
    public string? ApiGroupId { get; set; }
    public string? NewApiGroupName { get; set; }

    public List<ApiGroup>? ApiGroups { get; set; }
}