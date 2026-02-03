using Arkive.Core.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Arkive.Data.Configurations;

public class AuditEntryConfig : IEntityTypeConfiguration<AuditEntry>
{
    public void Configure(EntityTypeBuilder<AuditEntry> builder)
    {
        builder.ToTable("AuditEntries");

        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id)
            .HasDefaultValueSql("NEWSEQUENTIALID()");

        builder.Property(e => e.MspOrgId)
            .IsRequired();

        builder.Property(e => e.ClientTenantId);

        builder.Property(e => e.ActorId)
            .IsRequired()
            .HasMaxLength(200);

        builder.Property(e => e.ActorName)
            .IsRequired()
            .HasMaxLength(200);

        builder.Property(e => e.Action)
            .IsRequired()
            .HasMaxLength(100);

        builder.Property(e => e.Details)
            .HasColumnType("nvarchar(max)");

        builder.Property(e => e.CorrelationId)
            .HasMaxLength(200);

        builder.Property(e => e.Timestamp)
            .IsRequired()
            .HasDefaultValueSql("SYSDATETIMEOFFSET()");

        // Indexes for fast querying
        builder.HasIndex(e => new { e.ClientTenantId, e.Timestamp })
            .HasDatabaseName("IX_AuditEntries_ClientTenantId_Timestamp");

        builder.HasIndex(e => new { e.Action, e.Timestamp })
            .HasDatabaseName("IX_AuditEntries_Action_Timestamp");

        builder.HasIndex(e => new { e.MspOrgId, e.Timestamp })
            .HasDatabaseName("IX_AuditEntries_MspOrgId_Timestamp");

        // FK to MspOrganizations
        builder.HasOne(e => e.MspOrganization)
            .WithMany()
            .HasForeignKey(e => e.MspOrgId)
            .OnDelete(DeleteBehavior.Cascade);

        // FK to ClientTenants (optional — org-level actions have no tenant)
        builder.HasOne(e => e.ClientTenant)
            .WithMany()
            .HasForeignKey(e => e.ClientTenantId)
            .OnDelete(DeleteBehavior.NoAction);
    }
}
