using System.Net;
using System.Net.Http.Headers;
using System.Net.Mail;
using System.Text;
using HealthMonitorApp.Data;
using Newtonsoft.Json;
using SendGrid;
using SendGrid.Helpers.Mail;

namespace HealthMonitorApp.Services;

public class WarningService
{
    private readonly ApplicationDbContext _context;
    private readonly ILogger<WarningService> _logger;

    public WarningService(ILogger<WarningService> logger, ApplicationDbContext context)
    {
        _logger = logger;
        _context = context;
    }

    public async Task SendEmailAsync(string toEmail, string subject, string plainTextContent, string htmlContent)
    {
        // Hard-coded API key
        var apiKey = "";
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
        var apiUrl = "https://graph.facebook.com/v13.0/0539123125/messages"; // Replace with your API URL
        var accessToken =
            ""; // Replace with your access token

        using var client = new HttpClient();
        var payload = new
        {
            messaging_product = "whatsapp",
            to = 90556510250,
            type = "text",
            text = new { body = message }
        };

        var jsonPayload = JsonConvert.SerializeObject(payload);
        var content = new StringContent(jsonPayload, Encoding.UTF8, "application/json");

        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

        var response = await client.PostAsync(apiUrl, content);

        if (response.IsSuccessStatusCode)
            Console.WriteLine("WhatsApp message sent successfully.");
        else
            Console.WriteLine(
                $"Failed to send WhatsApp message: {response.StatusCode}. {response.Content.ReadAsStringAsync().Result}");
    }

    public async Task SendEmailViaExchangeAsync(string subject, string body)
    {
        var toEmail = "muhammet.bel@testinium.com";
        var username = "muhammet.bel@testinium.com";
        var smtpServer = "smtp-mail.outlook.com";
        var smtpPort = 587;
        var password = "";


        var setting = _context.Settings.FirstOrDefault();

        if (setting != null)
        {
            toEmail = setting.NotificationEmails;
            username = setting.SmtpUsername;
            password = setting.SmtpPassword;
            smtpServer = setting.SmtpServer;
            smtpPort = setting.SmtpPort;
        }


        var fromAddress = new MailAddress(username, "Health Monitor App");
        var toAddress = new MailAddress(toEmail);

        // Use manual settings as fallback
        var smtp = new SmtpClient
        {
            Host = smtpServer,
            Port = smtpPort,
            EnableSsl = true,
            DeliveryMethod = SmtpDeliveryMethod.Network,
            UseDefaultCredentials = false,
            Credentials = new NetworkCredential(username, password)
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