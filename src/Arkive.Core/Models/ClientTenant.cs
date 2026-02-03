using Arkive.Core.Enums;

namespace Arkive.Core.Models;

public class ClientTenant
{
    public Guid Id { get; set; }
    public Guid MspOrgId { get; set; }
    public string M365TenantId { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public TenantStatus Status { get; set; } = TenantStatus.Pending;
    public DateTimeOffset? ConnectedAt { get; set; }
    public DateTimeOffset? LastScannedAt { get; set; }
    public bool? AuditLogAvailable { get; set; }
    public string? ScanScheduleTimezone { get; set; }
    public bool ReviewFlagged { get; set; }
    public int? AutoApprovalDays { get; set; } = 7;
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }

    // Navigation properties
    public MspOrganization MspOrganization { get; set; } = null!;
    public ICollection<SharePointSite> SharePointSites { get; set; } = [];
    public ICollection<FileMetadata> FileMetadata { get; set; } = [];
}
