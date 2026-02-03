namespace Arkive.Core.Models;

/// <summary>
/// A point-in-time snapshot of a report, accessible via public token for 30 days.
/// </summary>
public class ReportSnapshot
{
    public Guid Id { get; set; }
    public Guid MspOrgId { get; set; }
    public Guid ClientTenantId { get; set; }

    /// <summary>Random URL-safe token for public access.</summary>
    public string Token { get; set; } = string.Empty;

    /// <summary>Tenant display name captured at snapshot time.</summary>
    public string TenantName { get; set; } = string.Empty;

    /// <summary>JSON-serialized report data (analytics + trends).</summary>
    public string ReportJson { get; set; } = string.Empty;

    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset ExpiresAt { get; set; }

    // Navigation properties
    public MspOrganization MspOrganization { get; set; } = null!;
    public ClientTenant ClientTenant { get; set; } = null!;
}
