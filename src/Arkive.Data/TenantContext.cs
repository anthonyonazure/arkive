namespace Arkive.Data;

/// <summary>
/// Scoped service that holds the current tenant identifiers for RLS SESSION_CONTEXT.
/// Populated by TenantContextMiddleware before any database access.
/// </summary>
public class TenantContext
{
    public string MspOrgId { get; set; } = string.Empty;
    public string? ClientTenantId { get; set; }
}
