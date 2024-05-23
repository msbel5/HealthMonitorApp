using HealthMonitorApp.Data;
using HealthMonitorApp.Services;
using HealthMonitorApp.Tools;
using HealthMonitorApp.Tools.Providers;
using Microsoft.EntityFrameworkCore;

// Add this if DataSeeder is in a different namespace
var builder = WebApplication.CreateBuilder(args);
var builtLog = "Built the application with the following services registered:\n";
// Add services to the container.
builder.Services.AddControllersWithViews();
builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseSqlite(builder.Configuration.GetConnectionString("DefaultConnection")));
builtLog += "ServiceType: ApplicationDbContext, Lifetime: Scoped, ImplementationType: None\n";
builder.Services.AddScoped<HealthCheckService>();
builtLog += "ServiceType: HealthCheckService, Lifetime: Scoped, ImplementationType: None\n";
builder.Services.AddScoped<AssertionService>();
builtLog += "ServiceType: AssertionService, Lifetime: Scoped, ImplementationType: None\n";
builder.Services.AddTransient<WarningService>();
builtLog += "ServiceType: WarningService, Lifetime: Transient, ImplementationType: None\n";
builder.Services.AddTransient<RepositoryService>();
builtLog += "ServiceType: RepositoryService, Lifetime: Transient, ImplementationType: None\n";
builder.Services.AddScoped<ReportHandler>();
builtLog += "ServiceType: ReportHandler, Lifetime: Scoped, ImplementationType: None\n";
builder.Services.AddTransient<DataSeeder>(); // Register DataSeeder as a service
builtLog += "ServiceType: DataSeeder, Lifetime: Transient, ImplementationType: None\n";
builder.Services.AddHostedService<HealthCheckHostedService>();
builtLog += "ServiceType: HealthCheckHostedService, Lifetime: Singleton, ImplementationType: None\n";
builder.Services.AddSignalR();
builtLog += "ServiceType: SignalR, Lifetime: Singleton, ImplementationType: None\n";

// Register the VCS and CodeCheck services
builder.Services.AddTransient<GitVcsProvider>(); // Assuming GitVcsProvider doesn't require interfaces to be registered
builtLog += "ServiceType: GitVcsProvider, Lifetime: Transient, ImplementationType: None\n";
builder.Services.AddTransient<VcsService>();
builtLog += "ServiceType: VcsService, Lifetime: Transient, ImplementationType: None\n";
builder.Services
    .AddScoped<ApplicationInspectorService>(); // Singleton to ensure one instance handles the installation check
builtLog += "ServiceType: ApplicationInspectorService, Lifetime: Scoped, ImplementationType: None\n";


var app = builder.Build();

var logger = app.Services.GetRequiredService<ILogger<Program>>();
logger.LogInformation(builtLog);


// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
    logger.LogInformation("Using ExceptionHandler and HSTS");
}

app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseRouting();
app.UseAuthorization();

app.UseEndpoints(endpoints =>
{
    endpoints.MapControllerRoute(
        "default",
        "{controller=HealthMonitor}/{action=Index}/{id?}");
    endpoints.MapHub<NotificationHub>("/notificationHub"); // Add this line to map the SignalR hub
});

logger.LogInformation("Configured the HTTP request pipeline");

// Ensure database is created
using (var scope = app.Services.CreateScope())
{
    var services = scope.ServiceProvider;
    try
    {
        var context = services.GetRequiredService<ApplicationDbContext>();
        logger.LogInformation("Application Database called");
        context.Database.EnsureCreated();
        logger.LogInformation("Ensured database created");
        // Ensure ApplicationInspector is installed
        var appInspectorService = services.GetRequiredService<ApplicationInspectorService>();
        logger.LogInformation("Application Inspector Service Called");
        appInspectorService.EnsureAppInspectorInstalledAsync().GetAwaiter().GetResult();
        logger.LogInformation("Ensured ApplicationInspector is installed");
        
    }
    catch (Exception ex)
    {
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
        Task.Run(async () => await seeder.SeedData()).GetAwaiter()
            .GetResult(); // Run the seeding and wait for it to complete
    }
});

app.Run("http://0.0.0.0:8080");