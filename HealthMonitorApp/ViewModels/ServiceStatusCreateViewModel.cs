using HealthMonitorApp.Models;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace HealthMonitorApp.ViewModels
{

    public class ServiceStatusCreateViewModel
    {
        [Required]
        public string ServiceName { get; set; }

        [Required]
        public int ExpectedStatusCode { get; set; }

        [Required]
        public string cURL { get; set; }

        public string? ApiGroupId { get; set; }
        public string? NewApiGroupName { get; set; }

        public List<ApiGroup>? ApiGroups { get; set; }
    }

}