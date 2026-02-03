using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Arkive.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddSharePointSitesTable : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "SharePointSites",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false, defaultValueSql: "NEWSEQUENTIALID()"),
                    ClientTenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    MspOrgId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SiteId = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    Url = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: false),
                    DisplayName = table.Column<string>(type: "nvarchar(400)", maxLength: 400, nullable: false),
                    StorageUsedBytes = table.Column<long>(type: "bigint", nullable: false, defaultValue: 0L),
                    IsSelected = table.Column<bool>(type: "bit", nullable: false, defaultValue: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false, defaultValueSql: "SYSDATETIMEOFFSET()"),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false, defaultValueSql: "SYSDATETIMEOFFSET()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SharePointSites", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SharePointSites_ClientTenants_ClientTenantId",
                        column: x => x.ClientTenantId,
                        principalTable: "ClientTenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_SharePointSites_ClientTenantId_IsSelected",
                table: "SharePointSites",
                columns: new[] { "ClientTenantId", "IsSelected" });

            migrationBuilder.CreateIndex(
                name: "IX_SharePointSites_ClientTenantId_SiteId",
                table: "SharePointSites",
                columns: new[] { "ClientTenantId", "SiteId" },
                unique: true);

            // Add RLS filter predicate on SharePointSites table
            migrationBuilder.Sql(@"
                ALTER SECURITY POLICY [Security].TenantIsolationPolicy
                    ADD FILTER PREDICATE [Security].fn_tenantAccessPredicate(MspOrgId) ON dbo.SharePointSites
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Remove RLS filter predicate before dropping table
            migrationBuilder.Sql(@"
                ALTER SECURITY POLICY [Security].TenantIsolationPolicy
                    DROP FILTER PREDICATE ON dbo.SharePointSites
            ");

            migrationBuilder.DropTable(
                name: "SharePointSites");
        }
    }
}
