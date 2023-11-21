using System;
using System.Diagnostics;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using CSScriptLib;
using HealthMonitorApp.Models;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json.Linq;
using iTextSharp.text.pdf;
using iTextSharp.text.pdf.parser;
using System.IO;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;

namespace HealthMonitorApp.Services
{
    public class HealthCheckService
    {
        private readonly ApplicationDbContext _context;
        private readonly ILogger<HealthCheckService> _logger;
        private const string CurlDelimiter = "&&&";  // Delimiter for separating multiple cURL commands

        public HealthCheckService(ApplicationDbContext context, ILogger<HealthCheckService> logger)
        {
            _context = context;
            _logger = logger;
        }

        public async Task CheckServiceStatusHealthAsync(ServiceStatus serviceStatus)
        {
            if (serviceStatus == null)
            {
                throw new ArgumentNullException(nameof(serviceStatus));
            }

            var endpoint = serviceStatus.ApiEndpoint;
            if (endpoint == null)
            {
                throw new InvalidOperationException($"No ApiEndpoint associated with ServiceStatus ID {serviceStatus.ID}.");
            }

            var stopwatch = new Stopwatch();
            stopwatch.Start();

            var isHealthy = true;
            var responseContentBuilder = new StringBuilder();
            var responseMessages = new List<HttpResponseMessage>();

            try
            {
                var curlCommands = endpoint.cURL.Split(new[] { CurlDelimiter }, StringSplitOptions.RemoveEmptyEntries);

                foreach (var curlCommand in curlCommands)
                {
                    var modifiedEndpoint = new ApiEndpoint { cURL = curlCommand, ExpectedStatusCode = endpoint.ExpectedStatusCode };
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
                    responseMessages.Add(response);
                    var responseString =  await response.Content.ReadAsStringAsync();  // Separated this line
                    responseString = response.StatusCode + " - " + responseString;
                    responseContentBuilder.AppendLine(responseString);  // and this line
                    isHealthy &= response.StatusCode == (HttpStatusCode)endpoint.ExpectedStatusCode;

                    // Add a delay if needed
                    if(curlCommands.Length>1){ await Task.Delay(TimeSpan.FromSeconds(3));}
                }
            }
            catch (Exception ex)
            {
                _logger.LogError($"Exception occurred: {ex.Message}");
                isHealthy = false;
                responseContentBuilder.AppendLine(ex.Message);
            }

            if (isHealthy && !string.IsNullOrEmpty(serviceStatus.AssertionScript))
            {
                foreach (var responseMessage in responseMessages)
                {
                    isHealthy &= await ExecuteCustomAssertion(responseMessage, serviceStatus.AssertionScript);
                }
            }

            serviceStatus.IsHealthy = isHealthy;
            serviceStatus.ResponseContent = responseContentBuilder.ToString();
            stopwatch.Stop();
            serviceStatus.CheckedAt = DateTime.Now;
            serviceStatus.ResponseTime = stopwatch.ElapsedMilliseconds / 1000.0;

            var history = new ServiceStatusHistory
            {
                ServiceStatusID = serviceStatus.ID,
                IsHealthy = serviceStatus.IsHealthy,
                CheckedAt = DateTime.Now,
                ResponseTime = serviceStatus.ResponseTime
            };

            _context.ServiceStatusHistories.Add(history);
            await _context.SaveChangesAsync();
        }

        private async Task<bool> ExecuteCustomAssertion(HttpResponseMessage response, string script)
        {
            try
            {
                var assertMethod = CSScript.Evaluator
                    .CreateDelegate($"bool AssertResponse(System.Net.Http.HttpResponseMessage responseMessage) {{ {script} }}");

                return (bool)assertMethod(response);
            }
            catch (Exception ex)
            {
                _logger.LogError($"Assertion script execution failed: {ex.Message}");
                return false;  // Depending on your policy, you might want to handle this differently
            }
        }

        private bool IsCurlCommand(string command)
        {
            return !string.IsNullOrWhiteSpace(command) && command.Trim().StartsWith("curl", StringComparison.OrdinalIgnoreCase);
        }

        private async Task<HttpResponseMessage> HandleLiteralUrlAsync(ApiEndpoint apiEndpoint)
        {
            using var httpClient = new HttpClient();
            return await httpClient.GetAsync(apiEndpoint.cURL);
        }

        private async Task<HttpResponseMessage> HandleCurlCommandAsync(ApiEndpoint endpoint)
        {
            var dynamicStringProcessor = new DynamicStringProcessor();
            var rawCurlCmd = endpoint.cURL.Replace("\\", string.Empty).Trim();

            // Validate the script
            if (!dynamicStringProcessor.ValidateScript(rawCurlCmd))
            {
                throw new InvalidOperationException("The script contains potentially harmful code.");
            }

            var curlCmd = dynamicStringProcessor.Process(rawCurlCmd);

            if (string.IsNullOrEmpty(curlCmd))
            {
                throw new ArgumentNullException(nameof(curlCmd), "cURL command cannot be null or empty.");
            }

            // Extracting URL
            var urlMatch = Regex.Match(curlCmd, @"'(https?:\/\/[^\s]*)'");
            if (!urlMatch.Success)
            {
                throw new InvalidOperationException("The cURL command does not contain a valid URL.");
            }

            var url = urlMatch.Groups[1].Value;

            // Extracting method
            var methodMatch = Regex.Match(curlCmd, @"-X\s+(\w+)|--request\s+'(\w+)'");
            var method = HttpMethod.Get;  // Default to GET

            if (methodMatch.Success)
            {
                method = new HttpMethod(methodMatch.Groups.Cast<Group>().Skip(1).First(g => g.Success).Value);
            }
            else
            {
                // Check if there is data, if so, then it's probably a POST request
                var postDataMatch = Regex.Match(curlCmd, @"--data-raw\s+'([^']+)'|--data\s+'([^']+)'");
                if (postDataMatch.Success)
                {
                    method = HttpMethod.Post;
                }
            }


            // Creating HTTP request
            var request = new HttpRequestMessage(method, url);

            // Extracting headers
            var headerMatches = Regex.Matches(curlCmd, @"-H\s+'([^']+)'|--header\s+'([^']+)'");
            bool hasContentTypeJson = false;
            bool hasAcceptHeader = false;
            bool hasConnectionHeader = false;
            foreach (Match headerMatch in headerMatches)
            {
                var headerString = headerMatch.Groups.Cast<Group>().Skip(1).First(g => g.Success).Value;
                var headerParts = headerString.Split(new[] { ": " }, StringSplitOptions.RemoveEmptyEntries);
                if (headerParts.Length == 2)
                {
                    request.Headers.TryAddWithoutValidation(headerParts[0], headerParts[1]);

                    if (headerParts[0].Equals("Content-Type", StringComparison.OrdinalIgnoreCase) &&
                        headerParts[1].Contains("application/json", StringComparison.OrdinalIgnoreCase))
                    {
                        hasContentTypeJson = true;
                    }
                    if (headerParts[0].Equals("Accept", StringComparison.OrdinalIgnoreCase))
                    {
                        hasAcceptHeader = true;
                    }
                    if (headerParts[0].Equals("Connection", StringComparison.OrdinalIgnoreCase))
                    {
                        hasConnectionHeader = true;
                    }
                }
            }

            // Auto-add headers if necessary
            if (hasContentTypeJson && !hasAcceptHeader && !request.Headers.Contains("Accept"))
            {
                request.Headers.TryAddWithoutValidation("Accept", "application/json");
            }

            if (method == HttpMethod.Post && hasContentTypeJson && !hasConnectionHeader && !request.Headers.Contains("Connection"))
            {
                request.Headers.Connection.Add("keep-alive");
            }


            // Extracting body
            var bodyMatch = Regex.Match(curlCmd, @"--data-raw\s+'([^']+)'|--data\s+'([^']+)'");
            if (bodyMatch.Success)
            {
                var bodyString = bodyMatch.Groups.Cast<Group>().Skip(1).First(g => g.Success).Value;
                request.Content = new StringContent(bodyString, Encoding.UTF8, "application/json");
            }

            // Sending HTTP request
            using var httpClient = new HttpClient();
            return await httpClient.SendAsync(request);
        }

    }
}

