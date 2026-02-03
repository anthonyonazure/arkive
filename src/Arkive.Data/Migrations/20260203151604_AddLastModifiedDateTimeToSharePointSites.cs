using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Arkive.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddLastModifiedDateTimeToSharePointSites : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "LastModifiedDateTime",
                table: "SharePointSites",
                type: "datetimeoffset",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "AutoApprovalDays",
                table: "ClientTenants",
                type: "int",
                nullable: true,
                defaultValue: 7);

            migrationBuilder.AddColumn<bool>(
                name: "ReviewFlagged",
                table: "ClientTenants",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "VetoReason",
                table: "ArchiveOperations",
                type: "nvarchar(1000)",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "VetoedAt",
                table: "ArchiveOperations",
                type: "datetimeoffset",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "VetoedBy",
                table: "ArchiveOperations",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "AuditEntries",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false, defaultValueSql: "NEWSEQUENTIALID()"),
                    MspOrgId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ClientTenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ActorId = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    ActorName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Action = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Details = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CorrelationId = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    Timestamp = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false, defaultValueSql: "SYSDATETIMEOFFSET()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AuditEntries", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AuditEntries_ClientTenants_ClientTenantId",
                        column: x => x.ClientTenantId,
                        principalTable: "ClientTenants",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_AuditEntries_MspOrganizations_MspOrgId",
                        column: x => x.MspOrgId,
                        principalTable: "MspOrganizations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "MonthlySavingsSnapshots",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false, defaultValueSql: "NEWSEQUENTIALID()"),
                    MspOrgId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ClientTenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Month = table.Column<string>(type: "nvarchar(7)", maxLength: 7, nullable: false),
                    TotalStorageBytes = table.Column<long>(type: "bigint", nullable: false),
                    ArchivedStorageBytes = table.Column<long>(type: "bigint", nullable: false),
                    StaleStorageBytes = table.Column<long>(type: "bigint", nullable: false),
                    SavingsAchieved = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    SavingsPotential = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    CapturedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false, defaultValueSql: "SYSDATETIMEOFFSET()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MonthlySavingsSnapshots", x => x.Id);
                    table.ForeignKey(
                        name: "FK_MonthlySavingsSnapshots_ClientTenants_ClientTenantId",
                        column: x => x.ClientTenantId,
                        principalTable: "ClientTenants",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_MonthlySavingsSnapshots_MspOrganizations_MspOrgId",
                        column: x => x.MspOrgId,
                        principalTable: "MspOrganizations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ReportSnapshots",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false, defaultValueSql: "NEWSEQUENTIALID()"),
                    MspOrgId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ClientTenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Token = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    TenantName = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    ReportJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false, defaultValueSql: "SYSDATETIMEOFFSET()"),
                    ExpiresAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ReportSnapshots", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ReportSnapshots_ClientTenants_ClientTenantId",
                        column: x => x.ClientTenantId,
                        principalTable: "ClientTenants",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_ReportSnapshots_MspOrganizations_MspOrgId",
                        column: x => x.MspOrgId,
                        principalTable: "MspOrganizations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AuditEntries_Action_Timestamp",
                table: "AuditEntries",
                columns: new[] { "Action", "Timestamp" });

            migrationBuilder.CreateIndex(
                name: "IX_AuditEntries_ClientTenantId_Timestamp",
                table: "AuditEntries",
                columns: new[] { "ClientTenantId", "Timestamp" });

            migrationBuilder.CreateIndex(
                name: "IX_AuditEntries_MspOrgId_Timestamp",
                table: "AuditEntries",
                columns: new[] { "MspOrgId", "Timestamp" });

            migrationBuilder.CreateIndex(
                name: "IX_MonthlySavingsSnapshots_ClientTenantId",
                table: "MonthlySavingsSnapshots",
                column: "ClientTenantId");

            migrationBuilder.CreateIndex(
                name: "IX_MonthlySavingsSnapshots_Org_Month",
                table: "MonthlySavingsSnapshots",
                columns: new[] { "MspOrgId", "Month" });

            migrationBuilder.CreateIndex(
                name: "IX_MonthlySavingsSnapshots_Org_Tenant_Month",
                table: "MonthlySavingsSnapshots",
                columns: new[] { "MspOrgId", "ClientTenantId", "Month" },
                unique: true,
                filter: "[ClientTenantId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_ReportSnapshots_ClientTenantId",
                table: "ReportSnapshots",
                column: "ClientTenantId");

            migrationBuilder.CreateIndex(
                name: "IX_ReportSnapshots_Org_Tenant",
                table: "ReportSnapshots",
                columns: new[] { "MspOrgId", "ClientTenantId" });

            migrationBuilder.CreateIndex(
                name: "IX_ReportSnapshots_Token",
                table: "ReportSnapshots",
                column: "Token",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AuditEntries");

            migrationBuilder.DropTable(
                name: "MonthlySavingsSnapshots");

            migrationBuilder.DropTable(
                name: "ReportSnapshots");

            migrationBuilder.DropColumn(
                name: "LastModifiedDateTime",
                table: "SharePointSites");

            migrationBuilder.DropColumn(
                name: "AutoApprovalDays",
                table: "ClientTenants");

            migrationBuilder.DropColumn(
                name: "ReviewFlagged",
                table: "ClientTenants");

            migrationBuilder.DropColumn(
                name: "VetoReason",
                table: "ArchiveOperations");

            migrationBuilder.DropColumn(
                name: "VetoedAt",
                table: "ArchiveOperations");

            migrationBuilder.DropColumn(
                name: "VetoedBy",
                table: "ArchiveOperations");
        }
    }
}
