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
            // Must drop the security policy before altering the function it references
            migrationBuilder.Sql("DROP SECURITY POLICY IF EXISTS [Security].TenantIsolationPolicy");

            // Update RLS predicate to allow system operations (timer triggers, Service Bus triggers)
            // when SESSION_CONTEXT('MspOrgId') is not set (NULL).
            migrationBuilder.Sql(@"
                ALTER FUNCTION [Security].fn_tenantAccessPredicate(@MspOrgId uniqueidentifier)
                RETURNS TABLE
                WITH SCHEMABINDING
                AS
                RETURN SELECT 1 AS accessResult
                WHERE @MspOrgId = CAST(SESSION_CONTEXT(N'MspOrgId') AS uniqueidentifier)
                   OR SESSION_CONTEXT(N'MspOrgId') IS NULL
            ");

            // Recreate the security policy with all current filter predicates
            migrationBuilder.Sql(@"
                CREATE SECURITY POLICY [Security].TenantIsolationPolicy
                    ADD FILTER PREDICATE [Security].fn_tenantAccessPredicate(MspOrgId) ON dbo.Users,
                    ADD FILTER PREDICATE [Security].fn_tenantAccessPredicate(MspOrgId) ON dbo.ClientTenants,
                    ADD FILTER PREDICATE [Security].fn_tenantAccessPredicate(MspOrgId) ON dbo.SharePointSites,
                    ADD FILTER PREDICATE [Security].fn_tenantAccessPredicate(MspOrgId) ON dbo.FileMetadata
                WITH (STATE = ON)
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Drop the policy, revert to strict RLS, recreate policy
            migrationBuilder.Sql("DROP SECURITY POLICY IF EXISTS [Security].TenantIsolationPolicy");

            migrationBuilder.Sql(@"
                ALTER FUNCTION [Security].fn_tenantAccessPredicate(@MspOrgId uniqueidentifier)
                RETURNS TABLE
                WITH SCHEMABINDING
                AS
                RETURN SELECT 1 AS accessResult
                WHERE @MspOrgId = CAST(SESSION_CONTEXT(N'MspOrgId') AS uniqueidentifier)
            ");

            migrationBuilder.Sql(@"
                CREATE SECURITY POLICY [Security].TenantIsolationPolicy
                    ADD FILTER PREDICATE [Security].fn_tenantAccessPredicate(MspOrgId) ON dbo.Users,
                    ADD FILTER PREDICATE [Security].fn_tenantAccessPredicate(MspOrgId) ON dbo.ClientTenants,
                    ADD FILTER PREDICATE [Security].fn_tenantAccessPredicate(MspOrgId) ON dbo.SharePointSites,
                    ADD FILTER PREDICATE [Security].fn_tenantAccessPredicate(MspOrgId) ON dbo.FileMetadata
                WITH (STATE = ON)
            ");
        }
    }
}
