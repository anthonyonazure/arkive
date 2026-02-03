using Arkive.Core.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Arkive.Data.Configurations;

public class MonthlySavingsSnapshotConfig : IEntityTypeConfiguration<MonthlySavingsSnapshot>
{
    public void Configure(EntityTypeBuilder<MonthlySavingsSnapshot> builder)
    {
        builder.ToTable("MonthlySavingsSnapshots");

        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id)
            .HasDefaultValueSql("NEWSEQUENTIALID()");

        builder.Property(e => e.MspOrgId)
            .IsRequired();

        builder.Property(e => e.ClientTenantId);

        builder.Property(e => e.Month)
            .IsRequired()
            .HasMaxLength(7); // "2026-01"

        builder.Property(e => e.TotalStorageBytes);
        builder.Property(e => e.ArchivedStorageBytes);
        builder.Property(e => e.StaleStorageBytes);

        builder.Property(e => e.SavingsAchieved)
            .HasColumnType("decimal(18,2)");

        builder.Property(e => e.SavingsPotential)
            .HasColumnType("decimal(18,2)");

        builder.Property(e => e.CapturedAt)
            .IsRequired()
            .HasDefaultValueSql("SYSDATETIMEOFFSET()");

        // Unique constraint: one snapshot per org + tenant + month
        builder.HasIndex(e => new { e.MspOrgId, e.ClientTenantId, e.Month })
            .IsUnique()
            .HasDatabaseName("IX_MonthlySavingsSnapshots_Org_Tenant_Month");

        // Query by org + month for org-level reports
        builder.HasIndex(e => new { e.MspOrgId, e.Month })
            .HasDatabaseName("IX_MonthlySavingsSnapshots_Org_Month");

        // FK to MspOrganizations
        builder.HasOne(e => e.MspOrganization)
            .WithMany()
            .HasForeignKey(e => e.MspOrgId)
            .OnDelete(DeleteBehavior.Cascade);

        // FK to ClientTenants (optional — org-level snapshots have no tenant)
        builder.HasOne(e => e.ClientTenant)
            .WithMany()
            .HasForeignKey(e => e.ClientTenantId)
            .OnDelete(DeleteBehavior.NoAction);
    }
}
