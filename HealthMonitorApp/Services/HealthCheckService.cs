using System.Diagnostics;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using HealthMonitorApp.Data;
using HealthMonitorApp.Models;

namespace HealthMonitorApp.Services;

public class HealthCheckService
{
    private const string Delimiter = "&&&"; // Delimiter for separating multiple cURL commands
    private readonly AssertionService _assertionService;
    private readonly ApplicationDbContext _context;
    private readonly ILogger<HealthCheckService> _logger;
    private readonly WarningService _warningService;

    public HealthCheckService(ApplicationDbContext context, ILogger<HealthCheckService> logger,
        AssertionService assertionService, WarningService warningService)
    {
        _context = context;
        _logger = logger;
        _assertionService = assertionService;
        _warningService = warningService;
    }


    public async Task CheckServiceStatusHealthAsync(ServiceStatus serviceStatus)
    {
        if (serviceStatus == null) throw new ArgumentNullException(nameof(serviceStatus));

        var endpoint = serviceStatus.ApiEndpoint;
        if (endpoint == null)
            throw new InvalidOperationException($"No ApiEndpoint associated with ServiceStatus Id {serviceStatus.Id}.");

        var stopwatch = new Stopwatch();
        stopwatch.Start();

        var isHealthy = true;
        var responseContentBuilder = new StringBuilder();
        var responseMessages = new List<HttpResponseMessage>();


        try
        {
            var curlCommands = endpoint.cURL.Split(new[] { Delimiter }, StringSplitOptions.RemoveEmptyEntries);

            foreach (var curlCommand in curlCommands)
            {
                var modifiedEndpoint = new ApiEndpoint
                {
                    cURL = curlCommand,
                    ExpectedStatusCode = endpoint.ExpectedStatusCode,
                    Id = endpoint.Id,
                    IsAuthorized = endpoint.IsAuthorized,
                    IsOpen = endpoint.IsOpen,
                    Name = endpoint.Name,
                    ServiceStatusId = endpoint.ServiceStatusId,
                    ApiGroupId = endpoint.ApiGroupId,
                    Annotations = endpoint.Annotations,
                    ApiGroup = endpoint.ApiGroup,
                    ServiceStatus = endpoint.ServiceStatus
                };
                HttpResponseMessage response;

                if (IsCurlCommand(modifiedEndpoint.cURL))
                {
                    _logger.LogInformation("Detected cURL command. Handling cURL.");
                    response = await HandleCurlCommandAsync(modifiedEndpoint);
                }
                else
                {
                    _logger.LogInformation("Detected literal URL. Handling literal URL.");
                    response = await HandleLiteralUrlAsync(modifiedEndpoint);
                }

                var encoding = response.Content.Headers.ContentType?.CharSet ?? "UTF-8";


                responseMessages.Add(response);
                var responseString = await response.Content.ReadAsStringAsync();
                responseString = "\t" + response.StatusCode + Environment.NewLine + responseString;
                if (responseMessages.Count > 1)
                    responseString = Environment.NewLine + Delimiter + Environment.NewLine + Environment.NewLine +
                                     responseString;
                responseContentBuilder.AppendLine(responseString); // and this line
                isHealthy &= response.StatusCode == (HttpStatusCode)endpoint.ExpectedStatusCode;

                // Add a delay if needed
                if (curlCommands.Length > 1) await Task.Delay(TimeSpan.FromSeconds(3));
            }
        }
        catch (Exception ex)
        {
            _logger.LogError($"Exception occurred: {ex.Message}");
            isHealthy = false;
            responseContentBuilder.AppendLine(ex.Message);
        }

        if (isHealthy && !string.IsNullOrEmpty(serviceStatus.AssertionScript))
            foreach (var responseMessage in responseMessages)
            {
                var result =
                    await _assertionService.ExecuteCustomAssertion(responseMessage, serviceStatus.AssertionScript);

                if (!result)
                {
                    _logger.LogInformation("Assertion failed for a response.");
                    isHealthy = false;
                    break; // Exit the loop if any assertion fails
                }

                _logger.LogInformation("Assertion passed for a response.");
            }

        serviceStatus.IsHealthy = isHealthy;
        serviceStatus.ResponseContent = responseContentBuilder.ToString();
        serviceStatus.AssertionScript = serviceStatus.AssertionScript;
        stopwatch.Stop();
        serviceStatus.CheckedAt = DateTime.Now;
        serviceStatus.ResponseTime = stopwatch.ElapsedMilliseconds / 1000.0;

        var history = new ServiceStatusHistory
        {
            ServiceStatusId = serviceStatus.Id,
            IsHealthy = serviceStatus.IsHealthy,
            CheckedAt = DateTime.Now,
            ResponseTime = serviceStatus.ResponseTime
        };

        _context.ServiceStatusHistories.Add(history);
        await _context.SaveChangesAsync();

        if (!serviceStatus.IsHealthy)
        {
            var emailBodyBuilder = new StringBuilder();
            emailBodyBuilder.AppendLine(
                $"The following service failed the health check: {serviceStatus.ApiEndpoint.Name}");
            emailBodyBuilder.AppendLine($"Response content: {serviceStatus.ResponseContent}");
            var emailBody = emailBodyBuilder.ToString();
            /*
            await _warningService.SendEmailViaExchangeAsync(
                "df.mesut.erdogmus@a101.com.tr",
                "Sos Health Check Failure",
                emailBody
            );

            await _warningService.SendEmailViaExchangeAsync(
                "mesut.erdogmus@testinium.com",
                "Sos Health Check Failure",
                emailBody
            );
            await _warningService.SendEmailViaExchangeAsync(
                "df.muhammed.sıddık.bel@a101.com.tr",
                "Sos Health Check Failure",
                emailBody
            );
            */
            await _warningService.SendEmailViaExchangeAsync(
                "Sos Health Check Failure",
                emailBody
            );
        }
    }

    private bool IsCurlCommand(string command)
    {
        return !string.IsNullOrWhiteSpace(command) &&
               command.Trim().StartsWith("curl", StringComparison.OrdinalIgnoreCase);
    }

    private async Task<HttpResponseMessage> HandleLiteralUrlAsync(ApiEndpoint apiEndpoint)
    {
        var dynamicStringProcessor = new DynamicStringProcessor(_context);
        apiEndpoint.cURL = apiEndpoint.cURL.Replace("\\", string.Empty).Trim();
        var serviceStatus = _context.ServiceStatuses.FirstOrDefault(ss => ss.ApiEndpointId == apiEndpoint.Id);
        // Validate the script
        if (serviceStatus != null && !dynamicStringProcessor.ValidateJavaScript(serviceStatus))
            throw new InvalidOperationException("The script contains potentially harmful code.");

        var cUrl = dynamicStringProcessor.Process(apiEndpoint);
        using var httpClient = new HttpClient();
        var response = await httpClient.GetAsync(cUrl);

        // Ensure the response is successful
        response.EnsureSuccessStatusCode();

        // Read the raw response stream
        var responseStream = await response.Content.ReadAsStreamAsync();

        // Determine the encoding from the 'Content-Type' header or default to UTF-8
        var charset = response.Content.Headers.ContentType?.CharSet ?? "UTF-8";
        Encoding encoding;
        try
        {
            encoding = Encoding.GetEncoding(charset);
        }
        catch (ArgumentException)
        {
            // If the specified charset is not supported, default to UTF-8
            encoding = Encoding.UTF8;
        }

        // Read the response content using the determined encoding
        using var reader = new StreamReader(responseStream, encoding);
        var content = await reader.ReadToEndAsync();
        // Replace the response content with the correctly encoded content
        response.Content = new StringContent(content, Encoding.UTF8, "application/json");

        return response;
    }

    private async Task<HttpResponseMessage> HandleCurlCommandAsync(ApiEndpoint apiEndpoint)
    {
        var dynamicStringProcessor = new DynamicStringProcessor(_context);
        apiEndpoint.cURL = apiEndpoint.cURL.Replace("\\", string.Empty).Trim();
        var serviceStatus = _context.ServiceStatuses.FirstOrDefault(ss => ss.ApiEndpointId == apiEndpoint.Id);
        // Validate the script
        if (serviceStatus != null && !dynamicStringProcessor.ValidateJavaScript(serviceStatus))
            throw new InvalidOperationException("The script contains potentially harmful code.");

        var cUrl = dynamicStringProcessor.Process(apiEndpoint);

        var segments = ParseCurlCommand(cUrl);

        var url = ExtractUrl(segments);
        var method = DetermineHttpMethod(segments);
        var headers = ExtractHeaders(segments);

        var request = new HttpRequestMessage(method, url);

        foreach (var header in headers) request.Headers.TryAddWithoutValidation(header.Key, header.Value);

        SetRequestBody(request, segments);

        using var httpClient = new HttpClient();
        return await httpClient.SendAsync(request);
    }

    private static string[] ParseCurlCommand(string cURL)
    {
        return Regex.Matches(cURL, @"[^\s""']+|""([^""]*)""|'([^']*)'")
            .Cast<Match>()
            .Select(m => m.Value.Trim('"').Trim('\''))
            .ToArray();
    }

    private static string ExtractUrl(string[] segments)
    {
        var url = segments.FirstOrDefault(s => Uri.IsWellFormedUriString(s, UriKind.Absolute));
        if (string.IsNullOrEmpty(url))
            throw new InvalidOperationException("The cURL command does not contain a valid URL.");
        return url;
    }

    private static HttpMethod DetermineHttpMethod(string[] segments)
    {
        var methodIndex = Array.IndexOf(segments, "-X");
        if (methodIndex != -1 && segments.Length > methodIndex + 1)
            return new HttpMethod(segments[methodIndex + 1].ToUpper());
        return HttpMethod.Get; // Default method if not specified
    }


    private static Dictionary<string, string> ExtractHeaders(string[] segments)
    {
        var headers = new Dictionary<string, string>();
        for (var i = 0; i < segments.Length; i++)
            if (segments[i] == "-H")
            {
                var headerParts = segments[i + 1].Split(new[] { ':' }, 2);
                if (headerParts.Length == 2) headers.Add(headerParts[0], headerParts[1].Trim());
                i++; // Skip the next segment since it's part of the current header
            }

        return headers;
    }


    private static void SetRequestBody(HttpRequestMessage request, string[] segments)
    {
        var method = request.Method;
        if (method == HttpMethod.Post || method == HttpMethod.Put || method == HttpMethod.Patch)
        {
            var dataSegmentIndex = Array.FindIndex(segments, s => s.StartsWith("--data") || s.StartsWith("--data-raw"));
            if (dataSegmentIndex != -1 && segments.Length > dataSegmentIndex + 1)
            {
                var bodyContent = segments[dataSegmentIndex + 1];
                request.Content = new StringContent(bodyContent, Encoding.UTF8, "application/json"); // Assuming JSON
            }
        }
    }
}