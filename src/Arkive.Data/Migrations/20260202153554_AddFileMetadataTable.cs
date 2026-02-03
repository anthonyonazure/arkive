using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Arkive.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddFileMetadataTable : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "LastScannedAt",
                table: "ClientTenants",
                type: "datetimeoffset",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "FileMetadata",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false, defaultValueSql: "NEWSEQUENTIALID()"),
                    ClientTenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    MspOrgId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SiteId = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    DriveId = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    ItemId = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    FileName = table.Column<string>(type: "nvarchar(400)", maxLength: 400, nullable: false),
                    FilePath = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: false),
                    FileType = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    SizeBytes = table.Column<long>(type: "bigint", nullable: false, defaultValue: 0L),
                    Owner = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false, defaultValueSql: "SYSDATETIMEOFFSET()"),
                    LastModifiedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false, defaultValueSql: "SYSDATETIMEOFFSET()"),
                    LastAccessedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    ArchiveStatus = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false, defaultValue: "Active"),
                    BlobTier = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    ScannedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false, defaultValueSql: "SYSDATETIMEOFFSET()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FileMetadata", x => x.Id);
                    table.ForeignKey(
                        name: "FK_FileMetadata_ClientTenants_ClientTenantId",
                        column: x => x.ClientTenantId,
                        principalTable: "ClientTenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_FileMetadata_ClientTenantId_ArchiveStatus",
                table: "FileMetadata",
                columns: new[] { "ClientTenantId", "ArchiveStatus" });

            migrationBuilder.CreateIndex(
                name: "IX_FileMetadata_ClientTenantId_LastAccessedAt",
                table: "FileMetadata",
                columns: new[] { "ClientTenantId", "LastAccessedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_FileMetadata_ClientTenantId_SiteId",
                table: "FileMetadata",
                columns: new[] { "ClientTenantId", "SiteId" });

            migrationBuilder.CreateIndex(
                name: "IX_FileMetadata_ClientTenantId_SiteId_ItemId",
                table: "FileMetadata",
                columns: new[] { "ClientTenantId", "SiteId", "ItemId" },
                unique: true);

            // Add RLS filter predicate on FileMetadata table
            migrationBuilder.Sql(@"
                ALTER SECURITY POLICY [Security].TenantIsolationPolicy
                    ADD FILTER PREDICATE [Security].fn_tenantAccessPredicate(MspOrgId) ON dbo.FileMetadata
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Remove RLS filter predicate before dropping table
            migrationBuilder.Sql(@"
                ALTER SECURITY POLICY [Security].TenantIsolationPolicy
                    DROP FILTER PREDICATE ON dbo.FileMetadata
            ");

            migrationBuilder.DropTable(
                name: "FileMetadata");

            migrationBuilder.DropColumn(
                name: "LastScannedAt",
                table: "ClientTenants");
        }
    }
}
