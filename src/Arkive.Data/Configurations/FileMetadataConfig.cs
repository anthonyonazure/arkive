using Arkive.Core.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Arkive.Data.Configurations;

public class FileMetadataConfig : IEntityTypeConfiguration<FileMetadata>
{
    public void Configure(EntityTypeBuilder<FileMetadata> builder)
    {
        builder.ToTable("FileMetadata");

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

        builder.Property(e => e.DriveId)
            .IsRequired()
            .HasMaxLength(500);

        builder.Property(e => e.ItemId)
            .IsRequired()
            .HasMaxLength(500);

        builder.Property(e => e.FileName)
            .IsRequired()
            .HasMaxLength(400);

        builder.Property(e => e.FilePath)
            .IsRequired()
            .HasMaxLength(2000);

        builder.Property(e => e.FileType)
            .IsRequired()
            .HasMaxLength(50);

        builder.Property(e => e.SizeBytes)
            .IsRequired()
            .HasDefaultValue(0L);

        builder.Property(e => e.Owner)
            .HasMaxLength(500);

        builder.Property(e => e.CreatedAt)
            .IsRequired()
            .HasDefaultValueSql("SYSDATETIMEOFFSET()");

        builder.Property(e => e.LastModifiedAt)
            .IsRequired()
            .HasDefaultValueSql("SYSDATETIMEOFFSET()");

        builder.Property(e => e.ArchiveStatus)
            .IsRequired()
            .HasMaxLength(50)
            .HasDefaultValue("Active");

        builder.Property(e => e.BlobTier)
            .HasMaxLength(50);

        builder.Property(e => e.ScannedAt)
            .IsRequired()
            .HasDefaultValueSql("SYSDATETIMEOFFSET()");

        // Prevent duplicate file entries per tenant
        builder.HasIndex(e => new { e.ClientTenantId, e.SiteId, e.ItemId })
            .IsUnique()
            .HasDatabaseName("IX_FileMetadata_ClientTenantId_SiteId_ItemId");

        // Fast query for filtering by archive status
        builder.HasIndex(e => new { e.ClientTenantId, e.ArchiveStatus })
            .HasDatabaseName("IX_FileMetadata_ClientTenantId_ArchiveStatus");

        // Fast query for staleness queries
        builder.HasIndex(e => new { e.ClientTenantId, e.LastAccessedAt })
            .HasDatabaseName("IX_FileMetadata_ClientTenantId_LastAccessedAt");

        // Fast query for per-site file listing
        builder.HasIndex(e => new { e.ClientTenantId, e.SiteId })
            .HasDatabaseName("IX_FileMetadata_ClientTenantId_SiteId");

        // FK to ClientTenants
        builder.HasOne(e => e.ClientTenant)
            .WithMany(t => t.FileMetadata)
            .HasForeignKey(e => e.ClientTenantId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
