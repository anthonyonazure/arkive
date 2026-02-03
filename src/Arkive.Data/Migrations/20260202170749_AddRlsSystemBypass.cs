using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Arkive.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddRlsSystemBypass : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Update RLS predicate to allow system operations (timer triggers, Service Bus triggers)
            // when SESSION_CONTEXT('MspOrgId') is not set (NULL).
            // Security model:
            //   - HTTP requests: Middleware always sets SESSION_CONTEXT → RLS filters by MSP org
            //   - System triggers: No middleware → SESSION_CONTEXT NULL → Full access (appropriate for system ops)
            migrationBuilder.Sql(@"
                ALTER FUNCTION [Security].fn_tenantAccessPredicate(@MspOrgId uniqueidentifier)
                RETURNS TABLE
                WITH SCHEMABINDING
                AS
                RETURN SELECT 1 AS accessResult
                WHERE @MspOrgId = CAST(SESSION_CONTEXT(N'MspOrgId') AS uniqueidentifier)
                   OR SESSION_CONTEXT(N'MspOrgId') IS NULL
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Revert to strict RLS without system bypass
            migrationBuilder.Sql(@"
                ALTER FUNCTION [Security].fn_tenantAccessPredicate(@MspOrgId uniqueidentifier)
                RETURNS TABLE
                WITH SCHEMABINDING
                AS
                RETURN SELECT 1 AS accessResult
                WHERE @MspOrgId = CAST(SESSION_CONTEXT(N'MspOrgId') AS uniqueidentifier)
            ");
        }
    }
}
