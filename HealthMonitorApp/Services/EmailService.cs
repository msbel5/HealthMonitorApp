using SendGrid;
using SendGrid.Helpers.Mail;
using System;
using System.Threading.Tasks;

namespace HealthMonitorApp.Services
{
    public class EmailService
    {
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

            if (response.StatusCode == System.Net.HttpStatusCode.Accepted)
            {
                Console.WriteLine("Email sent successfully.");
            }
            else
            {
                Console.WriteLine($"Failed to send email: {response.StatusCode}. {response.Body}");
            }
        }
    }
}