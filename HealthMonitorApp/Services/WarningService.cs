using SendGrid;
using SendGrid.Helpers.Mail;
using System.Net;
using System.Net.Mail;
using System.Text;
using Newtonsoft.Json;

namespace HealthMonitorApp.Services
{
    public class WarningService
    {
        
        private readonly ILogger<WarningService> _logger;

        public WarningService( ILogger<WarningService> logger)
        {
            _logger = logger;
        }
        
        public async Task SendEmailAsync(string toEmail, string subject, string plainTextContent, string htmlContent)
        {
            // Hard-coded API key
            var apiKey = "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";
            var client = new SendGridClient(apiKey);

            // Customize the sender email and name
            var from = new EmailAddress("df.muhammed.bel@a101.com.tr", "Health Monitor App");

            var to = new EmailAddress(toEmail);
            var msg = MailHelper.CreateSingleEmail(from, to, subject, plainTextContent, htmlContent);

            var response = await client.SendEmailAsync(msg);

            if (response.StatusCode == HttpStatusCode.Accepted)
            {
                _logger.LogInformation("Email sent successfully.");
            }
            else
            {
                Console.WriteLine($"Failed to send email: {response.StatusCode}. {response.Body}");
                _logger.LogError($"Failed to send email: {response.StatusCode}. {response.Body}");
            }
        }
        
        
        public async Task SendWhatsAppMessageAsync(string toWhatsAppNumber, string message)
        {
            var apiUrl = "https://graph.facebook.com/v13.0/yourApiUrl/messages"; // Replace with your API URL
            var accessToken = "yourAccessToken"; // Replace with your access token

            using var client = new HttpClient();
            var payload = new
            {
                messaging_product = "whatsapp",
                to = toWhatsAppNumber,
                type = "text",
                text = new { body = message }
            };

            var jsonPayload = JsonConvert.SerializeObject(payload);
            var content = new StringContent(jsonPayload, Encoding.UTF8, "application/json");

            client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", accessToken);

            var response = await client.PostAsync(apiUrl, content);

            if (response.IsSuccessStatusCode)
            {
                Console.WriteLine("WhatsApp message sent successfully.");
            }
            else
            {
                Console.WriteLine($"Failed to send WhatsApp message: {response.StatusCode}. {response.Content.ReadAsStringAsync().Result}");
            }
        }
        public async Task SendEmailViaExchangeAsync(string toEmail, string subject, string body)
        {
            var fromAddress = new MailAddress("muhammet.bel@testinium.com", "Health Monitor App");
            var toAddress = new MailAddress(toEmail);

            // Use manual settings as fallback
            var smtp = new SmtpClient
            {
                Host = "smtp-mail.outlook.com",
                Port = 587,
                EnableSsl = true,
                DeliveryMethod = SmtpDeliveryMethod.Network,
                UseDefaultCredentials = false,
                Credentials = new NetworkCredential(fromAddress.Address, "Agustos2023*") 
            };

            using (var message = new MailMessage(fromAddress, toAddress)
                   {
                       Subject = subject,
                       Body = body
                   })
            {
                try
                {
                    await smtp.SendMailAsync(message);
                    _logger.LogInformation("Email sent successfully via Microsoft Exchange.");
                }
                catch (Exception ex)
                {
                    _logger.LogError($"Failed to send email: {ex.Message}");
                }
            }
        }


    }
}