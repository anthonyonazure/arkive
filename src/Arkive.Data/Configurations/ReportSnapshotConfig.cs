using Arkive.Core.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Arkive.Data.Configurations;

public class ReportSnapshotConfig : IEntityTypeConfiguration<ReportSnapshot>
{
    public void Configure(EntityTypeBuilder<ReportSnapshot> builder)
    {
        builder.ToTable("ReportSnapshots");

        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id)
            .HasDefaultValueSql("NEWSEQUENTIALID()");

        builder.Property(e => e.MspOrgId)
            .IsRequired();

        builder.Property(e => e.ClientTenantId)
            .IsRequired();

        builder.Property(e => e.Token)
            .IsRequired()
            .HasMaxLength(64);

        builder.Property(e => e.TenantName)
            .IsRequired()
            .HasMaxLength(256);

        builder.Property(e => e.ReportJson)
            .IsRequired();

        builder.Property(e => e.CreatedAt)
            .IsRequired()
            .HasDefaultValueSql("SYSDATETIMEOFFSET()");

        builder.Property(e => e.ExpiresAt)
            .IsRequired();

        // Unique index on token for public lookups
        builder.HasIndex(e => e.Token)
            .IsUnique()
            .HasDatabaseName("IX_ReportSnapshots_Token");

        // Query by org + tenant for listing
        builder.HasIndex(e => new { e.MspOrgId, e.ClientTenantId })
            .HasDatabaseName("IX_ReportSnapshots_Org_Tenant");

        // FK to MspOrganizations
        builder.HasOne(e => e.MspOrganization)
            .WithMany()
            .HasForeignKey(e => e.MspOrgId)
            .OnDelete(DeleteBehavior.Cascade);

        // FK to ClientTenants
        builder.HasOne(e => e.ClientTenant)
            .WithMany()
            .HasForeignKey(e => e.ClientTenantId)
            .OnDelete(DeleteBehavior.NoAction);
    }
}
