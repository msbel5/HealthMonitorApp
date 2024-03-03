using System.Net.Mail;
using HealthMonitorApp.Models;
using HealthMonitorApp.Services;
using HealthMonitorApp.Data;  // Add this if DataSeeder is in a different namespace
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using System.Threading.Tasks;
using HealthMonitorApp.Tools;
using HealthMonitorApp.Tools.Providers;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllersWithViews();
builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseSqlite(builder.Configuration.GetConnectionString("DefaultConnection")));
builder.Services.AddScoped<HealthCheckService>();
builder.Services.AddScoped<AssertionService>();
builder.Services.AddScoped<WarningService>();
builder.Services.AddTransient<RepositoryService>();
builder.Services.AddScoped<ReportHandler>();
builder.Services.AddTransient<DataSeeder>();  // Register DataSeeder as a service
builder.Services.AddHostedService<HealthCheckHostedService>();
builder.Services.AddSignalR();

// Register the VCS and CodeCheck services
builder.Services.AddTransient<GitVcsProvider>(); // Assuming GitVcsProvider doesn't require interfaces to be registered
builder.Services.AddTransient<VcsService>();
builder.Services.AddTransient<ApplicationInspectorService>(); // Singleton to ensure one instance handles the installation check



var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();

app.UseRouting();

app.UseAuthorization();



app.UseEndpoints(endpoints =>
{
    endpoints.MapControllerRoute(
        name: "default",
        pattern: "{controller=HealthMonitor}/{action=Index}/{id?}");
    endpoints.MapHub<NotificationHub>("/notificationHub");  // Add this line to map the SignalR hub
});

// Ensure database is created
using (var scope = app.Services.CreateScope())
{
    var services = scope.ServiceProvider;
    try
    {
        var context = services.GetRequiredService<ApplicationDbContext>();
        context.Database.EnsureCreated();
        // Ensure ApplicationInspector is installed
        var appInspectorService = services.GetRequiredService<ApplicationInspectorService>();
        appInspectorService.EnsureInstalledAsync().GetAwaiter().GetResult();
    }
    catch (Exception ex)
    {
        var logger = services.GetRequiredService<ILogger<Program>>();
        logger.LogError(ex, "An error occurred during application startup.");
    }
}


// Seed data on application startup
var lifetime = app.Services.GetRequiredService<IHostApplicationLifetime>();
var scopeFactory = app.Services.GetRequiredService<IServiceScopeFactory>();

lifetime.ApplicationStarted.Register(() =>
{
    using (var scope = scopeFactory.CreateScope())
    {
        var seeder = scope.ServiceProvider.GetRequiredService<DataSeeder>();
        Task.Run(async () => await seeder.SeedData()).GetAwaiter().GetResult(); // Run the seeding and wait for it to complete
    }
});

app.Run();
