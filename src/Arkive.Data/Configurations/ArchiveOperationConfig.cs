using Arkive.Core.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Arkive.Data.Configurations;

public class ArchiveOperationConfig : IEntityTypeConfiguration<ArchiveOperation>
{
    public void Configure(EntityTypeBuilder<ArchiveOperation> builder)
    {
        builder.ToTable("ArchiveOperations");

        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id)
            .HasDefaultValueSql("NEWSEQUENTIALID()");

        builder.Property(e => e.ClientTenantId)
            .IsRequired();

        builder.Property(e => e.MspOrgId)
            .IsRequired();

        builder.Property(e => e.FileMetadataId)
            .IsRequired();

        builder.Property(e => e.OperationId)
            .IsRequired()
            .HasMaxLength(200);

        builder.Property(e => e.Action)
            .IsRequired()
            .HasMaxLength(50)
            .HasDefaultValue("Archive");

        builder.Property(e => e.SourcePath)
            .IsRequired()
            .HasMaxLength(2000);

        builder.Property(e => e.DestinationPath)
            .IsRequired()
            .HasMaxLength(2000);

        builder.Property(e => e.TargetTier)
            .IsRequired()
            .HasMaxLength(50)
            .HasDefaultValue("Cool");

        builder.Property(e => e.Status)
            .IsRequired()
            .HasMaxLength(50)
            .HasDefaultValue("Pending");

        builder.Property(e => e.ApprovedBy)
            .HasMaxLength(200);

        builder.Property(e => e.VetoedBy)
            .HasMaxLength(200);

        builder.Property(e => e.VetoReason)
            .HasMaxLength(1000);

        builder.Property(e => e.VetoedAt);

        builder.Property(e => e.ErrorMessage)
            .HasMaxLength(2000);

        builder.Property(e => e.CreatedAt)
            .IsRequired()
            .HasDefaultValueSql("SYSDATETIMEOFFSET()");

        builder.Property(e => e.CompletedAt);

        // Idempotency: unique operation ID per file
        builder.HasIndex(e => e.OperationId)
            .IsUnique()
            .HasDatabaseName("IX_ArchiveOperations_OperationId");

        // Fast query by tenant and status
        builder.HasIndex(e => new { e.ClientTenantId, e.Status })
            .HasDatabaseName("IX_ArchiveOperations_ClientTenantId_Status");

        // Fast query by file
        builder.HasIndex(e => e.FileMetadataId)
            .HasDatabaseName("IX_ArchiveOperations_FileMetadataId");

        // FK to ClientTenants
        builder.HasOne(e => e.ClientTenant)
            .WithMany()
            .HasForeignKey(e => e.ClientTenantId)
            .OnDelete(DeleteBehavior.Cascade);

        // FK to FileMetadata (no cascade — don't delete operations when file metadata is deleted)
        builder.HasOne(e => e.FileMetadata)
            .WithMany()
            .HasForeignKey(e => e.FileMetadataId)
            .OnDelete(DeleteBehavior.NoAction);
    }
}
