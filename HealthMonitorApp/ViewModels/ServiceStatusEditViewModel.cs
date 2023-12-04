using System.ComponentModel.DataAnnotations;
using HealthMonitorApp.Models;

namespace HealthMonitorApp.ViewModels;

public class ServiceStatusEditViewModel
{
    public int ID { get; set; }
    [Required]
    [StringLength(100, ErrorMessage = "Name must be under 100 characters.")]
    public string Name { get; set; }

    [Required]
    [Range(100, 599, ErrorMessage = "Expected status code must be a valid HTTP status code.")]
    public int ExpectedStatusCode { get; set; }

    [Required]
    [RegularExpression(@"^(https?:\/\/.+|curl\s+.+)$", ErrorMessage = "The CURL field must be a valid URL or cURL command.")]
    public string CURL { get; set; }

    public string? AssertionScript { get; set; }
    public int? ApiGroupID { get; set; }
    public string? NewApiGroupName { get; set; }
    public List<ApiGroup>? ApiGroups { get; set; }
}
