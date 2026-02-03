using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Arkive.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddArchiveRulesTable : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ArchiveRules",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false, defaultValueSql: "NEWSEQUENTIALID()"),
                    ClientTenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    MspOrgId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    RuleType = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    Criteria = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: false),
                    TargetTier = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false, defaultValue: "Cool"),
                    IsActive = table.Column<bool>(type: "bit", nullable: false, defaultValue: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false, defaultValueSql: "SYSDATETIMEOFFSET()"),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false, defaultValueSql: "SYSDATETIMEOFFSET()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ArchiveRules", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ArchiveRules_ClientTenants_ClientTenantId",
                        column: x => x.ClientTenantId,
                        principalTable: "ClientTenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ArchiveRules_ClientTenantId_IsActive",
                table: "ArchiveRules",
                columns: new[] { "ClientTenantId", "IsActive" });

            migrationBuilder.CreateIndex(
                name: "IX_ArchiveRules_ClientTenantId_RuleType",
                table: "ArchiveRules",
                columns: new[] { "ClientTenantId", "RuleType" });

            // Add RLS filter predicate on ArchiveRules table
            migrationBuilder.Sql(@"
                ALTER SECURITY POLICY [Security].TenantIsolationPolicy
                    ADD FILTER PREDICATE [Security].fn_tenantAccessPredicate(MspOrgId) ON dbo.ArchiveRules
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Remove RLS filter predicate before dropping table
            migrationBuilder.Sql(@"
                ALTER SECURITY POLICY [Security].TenantIsolationPolicy
                    DROP FILTER PREDICATE ON dbo.ArchiveRules
            ");

            migrationBuilder.DropTable(
                name: "ArchiveRules");
        }
    }
}
