using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Arkive.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddRowLevelSecurity : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Step 1: Create a dedicated schema for RLS objects
            migrationBuilder.Sql("EXEC('CREATE SCHEMA [Security]')");

            // Step 2: Create the predicate function for MspOrgId filtering
            // SCHEMABINDING prevents table alterations that would break RLS
            // Single quotes doubled inside EXEC('...')
            migrationBuilder.Sql(@"
                EXEC('
                    CREATE FUNCTION [Security].fn_tenantAccessPredicate(@MspOrgId uniqueidentifier)
                    RETURNS TABLE
                    WITH SCHEMABINDING
                    AS
                    RETURN SELECT 1 AS accessResult
                    WHERE @MspOrgId = CAST(SESSION_CONTEXT(N''MspOrgId'') AS uniqueidentifier)
                ')
            ");

            // Step 3: Create the security policy with FILTER predicates on tenant-scoped tables
            migrationBuilder.Sql(@"
                EXEC('
                    CREATE SECURITY POLICY [Security].TenantIsolationPolicy
                        ADD FILTER PREDICATE [Security].fn_tenantAccessPredicate(MspOrgId) ON dbo.Users,
                        ADD FILTER PREDICATE [Security].fn_tenantAccessPredicate(MspOrgId) ON dbo.ClientTenants
                    WITH (STATE = ON)
                ')
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Drop in reverse order: policy first, then function, then schema
            migrationBuilder.Sql("DROP SECURITY POLICY IF EXISTS [Security].TenantIsolationPolicy");
            migrationBuilder.Sql("DROP FUNCTION IF EXISTS [Security].fn_tenantAccessPredicate");
            migrationBuilder.Sql("DROP SCHEMA IF EXISTS [Security]");
        }
    }
}
