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
    public DbSet<ApiGroupVariable> ApiGroupVariables { get; set; }
    public DbSet<ApiEndpointVariable> ApiEndpointVariables { get; set; }
    public DbSet<RepositoryAnalysisVariable> RepositoryAnalysisVariables { get; set; }


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
        
        // Configure many-to-many for ApiGroup and Variable
        modelBuilder.Entity<ApiGroupVariable>().HasKey(agv => new { agv.ApiGroupId, agv.VariableId });
        modelBuilder.Entity<ApiGroupVariable>()
            .HasOne(agv => agv.ApiGroup)
            .WithMany(ag => ag.ApiGroupVariables)
            .HasForeignKey(agv => agv.ApiGroupId);
        modelBuilder.Entity<ApiGroupVariable>()
            .HasOne(agv => agv.Variable)
            .WithMany(v => v.ApiGroupVariables)
            .HasForeignKey(agv => agv.VariableId);

        // Configure many-to-many for ApiEndpoint and Variable
        modelBuilder.Entity<ApiEndpointVariable>().HasKey(aev => new { aev.ApiEndpointId, aev.VariableId });
        modelBuilder.Entity<ApiEndpointVariable>()
            .HasOne(aev => aev.ApiEndpoint)
            .WithMany(ae => ae.ApiEndpointVariables)
            .HasForeignKey(aev => aev.ApiEndpointId);
        modelBuilder.Entity<ApiEndpointVariable>()
            .HasOne(aev => aev.Variable)
            .WithMany(v => v.ApiEndpointVariables)
            .HasForeignKey(aev => aev.VariableId);

        // Configure many-to-many for RepositoryAnalysis and Variable
        modelBuilder.Entity<RepositoryAnalysisVariable>().HasKey(rav => new { rav.RepositoryAnalysisId, rav.VariableId });
        modelBuilder.Entity<RepositoryAnalysisVariable>()
            .HasOne(rav => rav.RepositoryAnalysis)
            .WithMany(ra => ra.RepositoryAnalysisVariables)
            .HasForeignKey(rav => rav.RepositoryAnalysisId);
        modelBuilder.Entity<RepositoryAnalysisVariable>()
            .HasOne(rav => rav.Variable)
            .WithMany(v => v.RepositoryAnalysisVariables)
            .HasForeignKey(rav => rav.VariableId);


    }
}