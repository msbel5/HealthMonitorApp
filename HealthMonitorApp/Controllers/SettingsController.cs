using Microsoft.AspNetCore.Mvc;
using HealthMonitorApp.Data;
using HealthMonitorApp.Models;
using HealthMonitorApp.ViewModels;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;

namespace HealthMonitorApp.Controllers
{
    public class SettingsController : Controller
    {
        private readonly ApplicationDbContext _context;

        public SettingsController(ApplicationDbContext context)
        {
            _context = context;
        }

        [HttpGet]
        public async Task<IActionResult> Index()
        {
            var settings = await _context.Settings.FirstOrDefaultAsync();
            if (settings == null)
            {
                settings = new Settings
                {
                    TestInterval = TimeSpan.FromMinutes(10), // Default to 10 minutes
                    NotificationEmails = "", // Default to empty string
                    SmtpServer = "smtp.example.com", // Default to a placeholder SMTP server
                    SmtpPort = 587, // Default to a common SMTP port
                    SmtpUsername = "", // Default to empty string
                    SmtpPassword = "" // Default to empty string
                };
                _context.Settings.Add(settings);
                await _context.SaveChangesAsync();
            }

            var model = new SettingsViewModel
            {
                TestIntervalMinutes = (int)settings.TestInterval.TotalMinutes,
                NotificationEmails = settings.NotificationEmails,
                SmtpServer = settings.SmtpServer,
                SmtpPort = settings.SmtpPort,
                SmtpUsername = settings.SmtpUsername,
                SmtpPassword = settings.SmtpPassword
            };

            return View(model);
        }

        [HttpPost]
        public async Task<IActionResult> Save(SettingsViewModel model)
        {
            if (!ModelState.IsValid)
            {
                return View("Index", model);
            }

            var settings = await _context.Settings.FirstOrDefaultAsync();
            if (settings == null)
            {
                settings = new Settings();
                _context.Settings.Add(settings);
            }

            settings.TestInterval = TimeSpan.FromMinutes(model.TestIntervalMinutes);
            settings.NotificationEmails = model.NotificationEmails;
            settings.SmtpServer = model.SmtpServer;
            settings.SmtpPort = model.SmtpPort;
            settings.SmtpUsername = model.SmtpUsername;
            settings.SmtpPassword = model.SmtpPassword;

            await _context.SaveChangesAsync();

            return RedirectToAction("Index");
        }
    }
}
