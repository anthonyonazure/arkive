namespace Arkive.Core.Models;

public class SharePointSite
{
    public Guid Id { get; set; }
    public Guid ClientTenantId { get; set; }
    public Guid MspOrgId { get; set; }
    public string SiteId { get; set; } = string.Empty;
    public string Url { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public long StorageUsedBytes { get; set; }
    public bool IsSelected { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }

    // Navigation properties
    public ClientTenant ClientTenant { get; set; } = null!;
}
