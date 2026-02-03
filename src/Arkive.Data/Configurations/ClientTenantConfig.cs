using Arkive.Core.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Arkive.Data.Configurations;

public class ClientTenantConfig : IEntityTypeConfiguration<ClientTenant>
{
    public void Configure(EntityTypeBuilder<ClientTenant> builder)
    {
        builder.ToTable("ClientTenants");

        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id)
            .HasDefaultValueSql("NEWSEQUENTIALID()");

        builder.Property(e => e.MspOrgId)
            .IsRequired();

        builder.Property(e => e.M365TenantId)
            .IsRequired()
            .HasMaxLength(36);

        builder.Property(e => e.DisplayName)
            .IsRequired()
            .HasMaxLength(200);

        builder.Property(e => e.Status)
            .IsRequired()
            .HasConversion<string>()
            .HasMaxLength(50);

        builder.Property(e => e.ConnectedAt)
            .IsRequired(false);

        builder.Property(e => e.ReviewFlagged)
            .IsRequired()
            .HasDefaultValue(false);

        builder.Property(e => e.AutoApprovalDays)
            .HasDefaultValue(7);

        builder.Property(e => e.CreatedAt)
            .IsRequired()
            .HasDefaultValueSql("SYSDATETIMEOFFSET()");

        builder.Property(e => e.UpdatedAt)
            .IsRequired()
            .HasDefaultValueSql("SYSDATETIMEOFFSET()");

        builder.HasIndex(e => new { e.MspOrgId, e.M365TenantId })
            .IsUnique()
            .HasDatabaseName("IX_ClientTenants_MspOrgId_M365TenantId");
    }
}
