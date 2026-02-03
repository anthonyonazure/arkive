namespace Arkive.Data.Extensions;

/// <summary>
/// Extension methods for populating TenantContext from various sources.
/// </summary>
public static class TenantContextExtensions
{
    /// <summary>
    /// Sets the TenantContext from raw claim values (typically extracted by middleware).
    /// </summary>
    public static void SetFromClaims(this TenantContext context, string mspOrgId, string? clientTenantId = null)
    {
        context.MspOrgId = mspOrgId;
        context.ClientTenantId = clientTenantId;
    }
}
