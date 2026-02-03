using Arkive.Core.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Arkive.Data.Configurations;

public class ArchiveRuleConfig : IEntityTypeConfiguration<ArchiveRule>
{
    public void Configure(EntityTypeBuilder<ArchiveRule> builder)
    {
        builder.ToTable("ArchiveRules");

        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id)
            .HasDefaultValueSql("NEWSEQUENTIALID()");

        builder.Property(e => e.ClientTenantId)
            .IsRequired();

        builder.Property(e => e.MspOrgId)
            .IsRequired();

        builder.Property(e => e.Name)
            .IsRequired()
            .HasMaxLength(200);

        builder.Property(e => e.RuleType)
            .IsRequired()
            .HasMaxLength(50);

        builder.Property(e => e.Criteria)
            .IsRequired()
            .HasMaxLength(4000);

        builder.Property(e => e.TargetTier)
            .IsRequired()
            .HasMaxLength(50)
            .HasDefaultValue("Cool");

        builder.Property(e => e.IsActive)
            .IsRequired()
            .HasDefaultValue(true);

        builder.Property(e => e.CreatedBy)
            .HasMaxLength(200);

        builder.Property(e => e.CreatedAt)
            .IsRequired()
            .HasDefaultValueSql("SYSDATETIMEOFFSET()");

        builder.Property(e => e.UpdatedAt)
            .IsRequired()
            .HasDefaultValueSql("SYSDATETIMEOFFSET()");

        // Fast query for rules by tenant
        builder.HasIndex(e => new { e.ClientTenantId, e.IsActive })
            .HasDatabaseName("IX_ArchiveRules_ClientTenantId_IsActive");

        // Fast query for rules by type
        builder.HasIndex(e => new { e.ClientTenantId, e.RuleType })
            .HasDatabaseName("IX_ArchiveRules_ClientTenantId_RuleType");

        // FK to ClientTenants
        builder.HasOne(e => e.ClientTenant)
            .WithMany()
            .HasForeignKey(e => e.ClientTenantId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
