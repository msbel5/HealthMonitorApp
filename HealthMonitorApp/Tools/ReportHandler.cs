using System.Text;
using HealthMonitorApp.Models;
using HealthMonitorApp.Services;
using HtmlAgilityPack;
using Newtonsoft.Json;

namespace HealthMonitorApp.Tools;

public class ReportHandler(RepositoryService repositoryService)
{
    public async Task ModifyAndSaveReport(RepositoryAnalysis? repositoryAnalysis)
    {
        var endpointJson = await repositoryService.ExtractControllersAndEndpointsAsJsonAsync(repositoryAnalysis);
        var apiGroups = JsonConvert.DeserializeObject<List<ApiGroup>>(endpointJson);

        // Assuming GetReportPath() returns the full path to the report file
        var reportPath = repositoryAnalysis.GetReportPath();

        // Load the HTML document
        var htmlDocument = new HtmlDocument();
        htmlDocument.Load(reportPath);

        // Remove the Application Inspector logo, GitHub logo, and the 'About' link
        RemoveNavbarItems(htmlDocument, repositoryAnalysis.Name);

        AddEndpointSummary(apiGroups, htmlDocument);


        // Save the modified HTML document back to the same file or a new file
        htmlDocument.Save(reportPath);

        // Additional logic for adding custom content can be implemented here
    }

    private void RemoveNavbarItems(HtmlDocument document, string repositoryName)
    {
        // Update the title of the HTML document
        var titleNode = document.DocumentNode.SelectSingleNode("//title");
        if (titleNode != null) titleNode.InnerHtml = $"Analysis Report for {repositoryName}";

        // Identify and remove the Application Inspector logo
        var logo = document.DocumentNode.SelectSingleNode("//img[@id='ms_logo']");
        logo?.Remove();

        // Identify and remove the GitHub link
        var githubLink =
            document.DocumentNode.SelectSingleNode("//a[@href='https://github.com/Microsoft/ApplicationInspector']");
        githubLink?.ParentNode?.Remove();

        // Identify and remove the 'About' link
        var aboutLink =
            document.DocumentNode.SelectSingleNode(
                "//a[@href='https://github.com/Microsoft/ApplicationInspector/wiki']");
        aboutLink?.ParentNode?.Remove();

        // Update the navbar brand with the repository name
        var navbarBrand = document.DocumentNode.SelectSingleNode("//span[@class='navbar-brand']");
        if (navbarBrand != null) navbarBrand.InnerHtml = repositoryName;
    }


    private void AddEndpointSummary(List<ApiGroup> apiGroups, HtmlDocument document)
    {
        var overviewSection = document.DocumentNode.SelectSingleNode("//div[@id='page__report_overview']");
        if (overviewSection != null)
        {
            overviewSection.RemoveAllChildren();

            var endpointOverview = new StringBuilder("<h2>Endpoint Summary</h2><div class='list-group'>");

            foreach (var apiGroup in apiGroups)
            {
                var controllerColor = apiGroup.IsAuthorized != null && apiGroup.IsAuthorized.Value
                    ? "list-group-item-secondary"
                    : "list-group-item-dark";

                // Append annotations for API Group if any
                if (apiGroup.Annotations != null && apiGroup.Annotations.Any())
                {
                    endpointOverview.Append("<div class='mb-2'><small class='text-muted'>");
                    var joinedControllerAnnotations = string.Join(", ", apiGroup.Annotations);
                    endpointOverview.Append(
                        $"<li style='margin-left: 20px; color:black;'>{joinedControllerAnnotations}</li>");
                    endpointOverview.Append("</small></div>");
                }

                endpointOverview.Append(
                    $"<a href='#' class='list-group-item {controllerColor} list-group-item-action'><i class='fas fa-cogs'></i> {apiGroup.Name}</a>");

                foreach (var endpoint in apiGroup.ApiEndpoints)
                {
                    var endpointColor = endpoint.IsOpen != null && endpoint.IsOpen.Value
                        ? "list-group-item-danger"
                        : "list-group-item-success";

                    // Append annotations for Endpoint if any
                    if (endpoint.Annotations != null && endpoint.Annotations.Any())
                    {
                        endpointOverview.Append("<div class='ml-4 mb-2'><small class='text-muted'>");
                        var joinedApiAnnotations = string.Join(", ", endpoint.Annotations);
                        endpointOverview.Append(
                            $"<li style='margin-left: 20px; color:black ;'>{joinedApiAnnotations}</li>");
                        endpointOverview.Append("</small></div>");
                    }

                    endpointOverview.Append(
                        $"<a href='#' class='list-group-item {endpointColor} list-group-item-action ml-3'><i class='fas fa-link'></i> {endpoint.Name}</a>");
                }
            }

            endpointOverview.Append("</div>");
            overviewSection.InnerHtml = endpointOverview.ToString();
        }
    }
}