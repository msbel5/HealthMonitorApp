using System.Diagnostics;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using HealthMonitorApp.Data;
using HealthMonitorApp.Models;



namespace HealthMonitorApp.Services
{
    public class HealthCheckService
    {
        private readonly ApplicationDbContext _context;
        private readonly ILogger<HealthCheckService> _logger;
        private readonly AssertionService _assertionService;
        private readonly WarningService _warningService;
        private const string Delimiter = "&&&";  // Delimiter for separating multiple cURL commands

        public HealthCheckService(ApplicationDbContext context, ILogger<HealthCheckService> logger, AssertionService assertionService, WarningService warningService)
        {
            _context = context;
            _logger = logger;
            _assertionService = assertionService;
            _warningService = warningService;
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
                var curlCommands = endpoint.cURL.Split(new[] { Delimiter }, StringSplitOptions.RemoveEmptyEntries);

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

                    var encoding = response.Content.Headers.ContentType?.CharSet ?? "UTF-8";

                    
                    responseMessages.Add(response);
                    var responseString = await response.Content.ReadAsStringAsync();
                    responseString = "\t" + response.StatusCode + Environment.NewLine + responseString;
                    if (responseMessages.Count > 1)
                    {
                        responseString = Environment.NewLine + Delimiter + Environment.NewLine + Environment.NewLine + responseString;
                    }
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
                    bool result = await _assertionService.ExecuteCustomAssertion(responseMessage, serviceStatus.AssertionScript);
        
                    if (!result)
                    {
                        _logger.LogInformation("Assertion failed for a response.");
                        isHealthy = false;
                        break; // Exit the loop if any assertion fails
                    }  
                    _logger.LogInformation("Assertion passed for a response.");
                    
                }
            }
            
            serviceStatus.IsHealthy = isHealthy;
            serviceStatus.ResponseContent = responseContentBuilder.ToString();
            serviceStatus.AssertionScript = serviceStatus.AssertionScript;
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
            
            if (!serviceStatus.IsHealthy)
            {
                StringBuilder emailBodyBuilder = new StringBuilder();
                emailBodyBuilder.AppendLine($"The following service failed the health check: {serviceStatus.ApiEndpoint.Name}");
                emailBodyBuilder.AppendLine($"Response content: {serviceStatus.ResponseContent}");
                String emailBody = emailBodyBuilder.ToString();
                
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
                await _warningService.SendEmailViaExchangeAsync(
                    "muhammet.bel@testinium.com",
                    "Sos Health Check Failure",
                    emailBody
                );
            }
        }

        private bool IsCurlCommand(string command)
        {
            return !string.IsNullOrWhiteSpace(command) && command.Trim().StartsWith("curl", StringComparison.OrdinalIgnoreCase);
        }

        private async Task<HttpResponseMessage> HandleLiteralUrlAsync(ApiEndpoint apiEndpoint)
        {
            using var httpClient = new HttpClient();
            var response = await httpClient.GetAsync(apiEndpoint.cURL);

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