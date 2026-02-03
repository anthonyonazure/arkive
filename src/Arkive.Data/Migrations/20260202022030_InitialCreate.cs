using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Arkive.Data.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "MspOrganizations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false, defaultValueSql: "NEWSEQUENTIALID()"),
                    Name = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    SubscriptionTier = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    EntraIdTenantId = table.Column<string>(type: "nvarchar(36)", maxLength: 36, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false, defaultValueSql: "SYSDATETIMEOFFSET()"),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false, defaultValueSql: "SYSDATETIMEOFFSET()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MspOrganizations", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ClientTenants",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false, defaultValueSql: "NEWSEQUENTIALID()"),
                    MspOrgId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    M365TenantId = table.Column<string>(type: "nvarchar(36)", maxLength: 36, nullable: false),
                    DisplayName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Status = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false, defaultValueSql: "SYSDATETIMEOFFSET()"),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false, defaultValueSql: "SYSDATETIMEOFFSET()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ClientTenants", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ClientTenants_MspOrganizations_MspOrgId",
                        column: x => x.MspOrgId,
                        principalTable: "MspOrganizations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Users",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false, defaultValueSql: "NEWSEQUENTIALID()"),
                    MspOrgId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    EntraIdObjectId = table.Column<string>(type: "nvarchar(36)", maxLength: 36, nullable: false),
                    Email = table.Column<string>(type: "nvarchar(320)", maxLength: 320, nullable: false),
                    DisplayName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Role = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false, defaultValueSql: "SYSDATETIMEOFFSET()"),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false, defaultValueSql: "SYSDATETIMEOFFSET()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Users", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Users_MspOrganizations_MspOrgId",
                        column: x => x.MspOrgId,
                        principalTable: "MspOrganizations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ClientTenants_MspOrgId_M365TenantId",
                table: "ClientTenants",
                columns: new[] { "MspOrgId", "M365TenantId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_MspOrganizations_EntraIdTenantId",
                table: "MspOrganizations",
                column: "EntraIdTenantId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Users_EntraIdObjectId",
                table: "Users",
                column: "EntraIdObjectId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Users_MspOrgId",
                table: "Users",
                column: "MspOrgId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ClientTenants");

            migrationBuilder.DropTable(
                name: "Users");

            migrationBuilder.DropTable(
                name: "MspOrganizations");
        }
    }
}
