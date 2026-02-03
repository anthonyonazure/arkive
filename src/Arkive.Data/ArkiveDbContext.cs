using Arkive.Core.Models;
using Microsoft.EntityFrameworkCore;

namespace Arkive.Data;

public class ArkiveDbContext : DbContext
{
    public ArkiveDbContext(DbContextOptions<ArkiveDbContext> options)
        : base(options)
    {
    }

    public DbSet<MspOrganization> MspOrganizations => Set<MspOrganization>();
    public DbSet<User> Users => Set<User>();
    public DbSet<ClientTenant> ClientTenants => Set<ClientTenant>();
    public DbSet<SharePointSite> SharePointSites => Set<SharePointSite>();
    public DbSet<FileMetadata> FileMetadata => Set<FileMetadata>();
    public DbSet<ArchiveRule> ArchiveRules => Set<ArchiveRule>();
    public DbSet<ArchiveOperation> ArchiveOperations => Set<ArchiveOperation>();
    public DbSet<AuditEntry> AuditEntries => Set<AuditEntry>();
    public DbSet<MonthlySavingsSnapshot> MonthlySavingsSnapshots => Set<MonthlySavingsSnapshot>();
    public DbSet<ReportSnapshot> ReportSnapshots => Set<ReportSnapshot>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.ApplyConfigurationsFromAssembly(typeof(ArkiveDbContext).Assembly);
    }

    public override int SaveChanges(bool acceptAllChangesOnSuccess)
    {
        SetUpdatedAtTimestamps();
        return base.SaveChanges(acceptAllChangesOnSuccess);
    }

    public override Task<int> SaveChangesAsync(bool acceptAllChangesOnSuccess, CancellationToken cancellationToken = default)
    {
        SetUpdatedAtTimestamps();
        return base.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken);
    }

    private void SetUpdatedAtTimestamps()
    {
        var now = DateTimeOffset.UtcNow;

        foreach (var entry in ChangeTracker.Entries())
        {
            if (entry.State != EntityState.Modified)
                continue;

            if (entry.Entity is MspOrganization org)
                org.UpdatedAt = now;
            else if (entry.Entity is User user)
                user.UpdatedAt = now;
            else if (entry.Entity is ClientTenant tenant)
                tenant.UpdatedAt = now;
            else if (entry.Entity is SharePointSite site)
                site.UpdatedAt = now;
            else if (entry.Entity is ArchiveRule rule)
                rule.UpdatedAt = now;
        }
    }
}
