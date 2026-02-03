using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Arkive.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddArchiveOperationsTable : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ArchiveOperations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false, defaultValueSql: "NEWSEQUENTIALID()"),
                    ClientTenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    MspOrgId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    FileMetadataId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OperationId = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Action = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false, defaultValue: "Archive"),
                    SourcePath = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: false),
                    DestinationPath = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: false),
                    TargetTier = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false, defaultValue: "Cool"),
                    Status = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false, defaultValue: "Pending"),
                    ApprovedBy = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    ErrorMessage = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false, defaultValueSql: "SYSDATETIMEOFFSET()"),
                    CompletedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ArchiveOperations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ArchiveOperations_ClientTenants_ClientTenantId",
                        column: x => x.ClientTenantId,
                        principalTable: "ClientTenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ArchiveOperations_FileMetadata_FileMetadataId",
                        column: x => x.FileMetadataId,
                        principalTable: "FileMetadata",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateIndex(
                name: "IX_ArchiveOperations_ClientTenantId_Status",
                table: "ArchiveOperations",
                columns: new[] { "ClientTenantId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_ArchiveOperations_FileMetadataId",
                table: "ArchiveOperations",
                column: "FileMetadataId");

            migrationBuilder.CreateIndex(
                name: "IX_ArchiveOperations_OperationId",
                table: "ArchiveOperations",
                column: "OperationId",
                unique: true);

            // RLS policy for tenant isolation
            migrationBuilder.Sql(@"
                ALTER SECURITY POLICY rls.tenantAccessPolicy
                    ADD FILTER PREDICATE rls.fn_tenantAccessPredicate(MspOrgId) ON dbo.ArchiveOperations,
                    ADD BLOCK PREDICATE rls.fn_tenantAccessPredicate(MspOrgId) ON dbo.ArchiveOperations;
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
                ALTER SECURITY POLICY rls.tenantAccessPolicy
                    DROP FILTER PREDICATE ON dbo.ArchiveOperations,
                    DROP BLOCK PREDICATE ON dbo.ArchiveOperations;
            ");

            migrationBuilder.DropTable(
                name: "ArchiveOperations");
        }
    }
}
