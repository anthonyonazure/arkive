using Arkive.Core.Enums;

namespace Arkive.Core.DTOs;

public class TenantSummaryDto
{
    public Guid Id { get; set; }
    public string DisplayName { get; set; } = string.Empty;
    public TenantStatus Status { get; set; }
    public DateTimeOffset? ConnectedAt { get; set; }
    public int SelectedSiteCount { get; set; }
    public long TotalStorageBytes { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
}
