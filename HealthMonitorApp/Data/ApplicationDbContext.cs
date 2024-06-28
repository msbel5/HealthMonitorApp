using HealthMonitorApp.Models;
using Microsoft.EntityFrameworkCore;

namespace HealthMonitorApp.Data;

public class ApplicationDbContext : DbContext
{
    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options) : base(options)
    {
    }

    public DbSet<ApiGroup> ApiGroups { get; set; }
    public DbSet<ApiEndpoint> ApiEndpoints { get; set; }
    public DbSet<ServiceStatus> ServiceStatuses { get; set; }
    public DbSet<ServiceStatusHistory> ServiceStatusHistories { get; set; }
    public DbSet<RepositoryAnalysis> RepositoryAnalysis { get; set; }
    public DbSet<Variable> Variables { get; set; }
    public DbSet<Settings> Settings { get; set; }


    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // ServiceStatus to ApiEndpoint (One-to-One)
        modelBuilder.Entity<ServiceStatus>()
            .HasOne(ss => ss.ApiEndpoint)
            .WithOne(ae => ae.ServiceStatus)
            .HasForeignKey<ApiEndpoint>(ae => ae.ServiceStatusId)
            .OnDelete(DeleteBehavior.Cascade);

        // ServiceStatus to ServiceStatusHistory (One-to-Many)
        modelBuilder.Entity<ServiceStatusHistory>()
            .HasOne(ssh => ssh.ServiceStatus)
            .WithMany(ss => ss.ServiceStatusHistories)
            .HasForeignKey(ssh => ssh.ServiceStatusId)
            .OnDelete(DeleteBehavior.Cascade);

        // ApiEndpoint to ApiGroup (Many-to-One, nullable)
        modelBuilder.Entity<ApiEndpoint>()
            .HasOne(ae => ae.ApiGroup)
            .WithMany(ag => ag.ApiEndpoints)
            .HasForeignKey(ae => ae.ApiGroupId)
            .OnDelete(DeleteBehavior.SetNull);

        // ApiGroups  to  RepositoryAnalysis (Many-to-One)
        modelBuilder.Entity<ApiGroup>()
            .HasOne(ag => ag.RepositoryAnalysis)
            .WithMany(ra => ra.ApiGroups)
            .HasForeignKey(ag => ag.RepositoryAnalysisId)
            .OnDelete(DeleteBehavior.SetNull);

        // Relationship between ApiGroup and Variable
        modelBuilder.Entity<Variable>()
            .HasOne(v => v.ApiGroup)
            .WithMany(ag => ag.Variables)
            .HasForeignKey(v => v.ApiGroupId)
            .OnDelete(DeleteBehavior.Restrict); // Adjust the DeleteBehavior as per your requirements

        // Relationship between Repository and Variable
        modelBuilder.Entity<Variable>()
            .HasOne(v => v.RepositoryAnalysis)
            .WithMany(r => r.Variables)
            .HasForeignKey(v => v.RepositoryAnalysisId)
            .OnDelete(DeleteBehavior.Restrict); // Adjust the DeleteBehavior as per your requirements


        var optionsBuilder = new DbContextOptionsBuilder<ApplicationDbContext>();
        optionsBuilder.EnableSensitiveDataLogging();
    }
}