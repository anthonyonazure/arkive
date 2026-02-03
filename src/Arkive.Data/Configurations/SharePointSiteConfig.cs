using Arkive.Core.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Arkive.Data.Configurations;

public class SharePointSiteConfig : IEntityTypeConfiguration<SharePointSite>
{
    public void Configure(EntityTypeBuilder<SharePointSite> builder)
    {
        builder.ToTable("SharePointSites");

        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id)
            .HasDefaultValueSql("NEWSEQUENTIALID()");

        builder.Property(e => e.ClientTenantId)
            .IsRequired();

        builder.Property(e => e.MspOrgId)
            .IsRequired();

        builder.Property(e => e.SiteId)
            .IsRequired()
            .HasMaxLength(500);

        builder.Property(e => e.Url)
            .IsRequired()
            .HasMaxLength(2000);

        builder.Property(e => e.DisplayName)
            .IsRequired()
            .HasMaxLength(400);

        builder.Property(e => e.StorageUsedBytes)
            .IsRequired()
            .HasDefaultValue(0L);

        builder.Property(e => e.IsSelected)
            .IsRequired()
            .HasDefaultValue(false);

        builder.Property(e => e.CreatedAt)
            .IsRequired()
            .HasDefaultValueSql("SYSDATETIMEOFFSET()");

        builder.Property(e => e.UpdatedAt)
            .IsRequired()
            .HasDefaultValueSql("SYSDATETIMEOFFSET()");

        // Prevent duplicate site entries per tenant
        builder.HasIndex(e => new { e.ClientTenantId, e.SiteId })
            .IsUnique()
            .HasDatabaseName("IX_SharePointSites_ClientTenantId_SiteId");

        // Fast query for selected sites
        builder.HasIndex(e => new { e.ClientTenantId, e.IsSelected })
            .HasDatabaseName("IX_SharePointSites_ClientTenantId_IsSelected");

        // FK to ClientTenants
        builder.HasOne(e => e.ClientTenant)
            .WithMany(t => t.SharePointSites)
            .HasForeignKey(e => e.ClientTenantId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
