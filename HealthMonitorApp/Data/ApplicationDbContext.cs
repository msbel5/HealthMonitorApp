using HealthMonitorApp.Models;
using Microsoft.EntityFrameworkCore;

namespace HealthMonitorApp.Data
{
    public class ApplicationDbContext : DbContext
    {
        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options) : base(options)
        {
        }

        public DbSet<ApiGroup> ApiGroups { get; set; }
        public DbSet<ApiEndpoint> ApiEndpoints { get; set; }
        public DbSet<ServiceStatus> ServiceStatuses { get; set; }
        public DbSet<ServiceStatusHistory> ServiceStatusHistories { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // ServiceStatus to ApiEndpoint (One-to-One)
            modelBuilder.Entity<ServiceStatus>()
                .HasOne(ss => ss.ApiEndpoint)
                .WithOne(ae => ae.ServiceStatus)
                .HasForeignKey<ApiEndpoint>(ae => ae.ServiceStatusID)
                .OnDelete(DeleteBehavior.Cascade);

            // ServiceStatus to ServiceStatusHistory (One-to-Many)
            modelBuilder.Entity<ServiceStatusHistory>()
                .HasOne(ssh => ssh.ServiceStatus)
                .WithMany(ss => ss.ServiceStatusHistories)
                .HasForeignKey(ssh => ssh.ServiceStatusID)
                .OnDelete(DeleteBehavior.Cascade);

            // ApiEndpoint to ApiGroup (Many-to-One, nullable)
            modelBuilder.Entity<ApiEndpoint>()
                .HasOne(ae => ae.ApiGroup)
                .WithMany(ag => ag.ApiEndpoints)
                .HasForeignKey(ae => ae.ApiGroupID)
                .OnDelete(DeleteBehavior.SetNull);
        }


    }
}