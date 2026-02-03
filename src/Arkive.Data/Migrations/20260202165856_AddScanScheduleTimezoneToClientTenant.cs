using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Arkive.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddScanScheduleTimezoneToClientTenant : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ScanScheduleTimezone",
                table: "ClientTenants",
                type: "nvarchar(max)",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ScanScheduleTimezone",
                table: "ClientTenants");
        }
    }
}
