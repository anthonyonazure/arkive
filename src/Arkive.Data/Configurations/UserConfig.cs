using Arkive.Core.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Arkive.Data.Configurations;

public class UserConfig : IEntityTypeConfiguration<User>
{
    public void Configure(EntityTypeBuilder<User> builder)
    {
        builder.ToTable("Users");

        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id)
            .HasDefaultValueSql("NEWSEQUENTIALID()");

        builder.Property(e => e.MspOrgId)
            .IsRequired();

        builder.Property(e => e.EntraIdObjectId)
            .IsRequired()
            .HasMaxLength(36);

        builder.Property(e => e.Email)
            .IsRequired()
            .HasMaxLength(320);

        builder.Property(e => e.DisplayName)
            .IsRequired()
            .HasMaxLength(200);

        builder.Property(e => e.Role)
            .IsRequired()
            .HasConversion<string>()
            .HasMaxLength(50);

        builder.Property(e => e.CreatedAt)
            .IsRequired()
            .HasDefaultValueSql("SYSDATETIMEOFFSET()");

        builder.Property(e => e.UpdatedAt)
            .IsRequired()
            .HasDefaultValueSql("SYSDATETIMEOFFSET()");

        builder.HasIndex(e => e.EntraIdObjectId)
            .IsUnique()
            .HasDatabaseName("IX_Users_EntraIdObjectId");

        builder.HasIndex(e => e.MspOrgId)
            .HasDatabaseName("IX_Users_MspOrgId");
    }
}
