using Arkive.Core.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Arkive.Data.Configurations;

public class MspOrganizationConfig : IEntityTypeConfiguration<MspOrganization>
{
    public void Configure(EntityTypeBuilder<MspOrganization> builder)
    {
        builder.ToTable("MspOrganizations");

        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id)
            .HasDefaultValueSql("NEWSEQUENTIALID()");

        builder.Property(e => e.Name)
            .IsRequired()
            .HasMaxLength(200);

        builder.Property(e => e.SubscriptionTier)
            .IsRequired()
            .HasConversion<string>()
            .HasMaxLength(50);

        builder.Property(e => e.EntraIdTenantId)
            .IsRequired()
            .HasMaxLength(36);

        builder.Property(e => e.CreatedAt)
            .IsRequired()
            .HasDefaultValueSql("SYSDATETIMEOFFSET()");

        builder.Property(e => e.UpdatedAt)
            .IsRequired()
            .HasDefaultValueSql("SYSDATETIMEOFFSET()");

        builder.HasIndex(e => e.EntraIdTenantId)
            .IsUnique()
            .HasDatabaseName("IX_MspOrganizations_EntraIdTenantId");

        // Relationships
        builder.HasMany(e => e.Users)
            .WithOne(e => e.MspOrganization)
            .HasForeignKey(e => e.MspOrgId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasMany(e => e.ClientTenants)
            .WithOne(e => e.MspOrganization)
            .HasForeignKey(e => e.MspOrgId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
