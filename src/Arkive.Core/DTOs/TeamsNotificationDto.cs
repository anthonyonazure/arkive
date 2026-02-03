using Arkive.Core.Interfaces;

namespace Arkive.Core.DTOs;

/// <summary>
/// Input for sending an approval notification to a site owner via Teams.
/// </summary>
public class ApprovalNotificationInput
{
    public Guid TenantId { get; set; }
    public Guid MspOrgId { get; set; }
    public string M365TenantId { get; set; } = string.Empty;
    public string SiteOwnerEmail { get; set; } = string.Empty;
    public string SiteOwnerAadId { get; set; } = string.Empty;
    public string SiteName { get; set; } = string.Empty;
    public string SiteId { get; set; } = string.Empty;
    public int FileCount { get; set; }
    public long TotalSizeBytes { get; set; }
    public string TargetTier { get; set; } = "Cool";
    public string OrchestrationInstanceId { get; set; } = string.Empty;
}

/// <summary>
/// Result of sending an approval notification.
/// </summary>
public class ApprovalNotificationResult
{
    public string SiteOwnerEmail { get; set; } = string.Empty;
    public string SiteId { get; set; } = string.Empty;
    public bool Delivered { get; set; }
    public string? ConversationId { get; set; }
    public string? ErrorMessage { get; set; }
    public int AttemptCount { get; set; }
}

/// <summary>
/// Input for grouping files by site owner for notification purposes.
/// </summary>
public class GroupFilesBySiteOwnerInput
{
    public Guid TenantId { get; set; }
    public Guid MspOrgId { get; set; }
    public List<ArchiveFileInput> Files { get; set; } = [];
}

/// <summary>
/// Input for transitioning ArchiveOperation records to AwaitingApproval status.
/// </summary>
public class SetAwaitingApprovalInput
{
    public Guid TenantId { get; set; }
    public List<string> SiteIds { get; set; } = [];
}

/// <summary>
/// Input for retrieving a tenant's auto-approval days setting.
/// </summary>
public class GetAutoApprovalDaysInput
{
    public Guid TenantId { get; set; }
}

/// <summary>
/// Input for auto-approving operations for a site when the approval timer expires.
/// </summary>
public class AutoApproveExpiredInput
{
    public Guid TenantId { get; set; }
    public string SiteId { get; set; } = string.Empty;
}

/// <summary>
/// A group of files belonging to a single site owner.
/// </summary>
public class SiteOwnerFileGroup
{
    public string SiteId { get; set; } = string.Empty;
    public string SiteName { get; set; } = string.Empty;
    public string OwnerEmail { get; set; } = string.Empty;
    public string OwnerAadId { get; set; } = string.Empty;
    public int FileCount { get; set; }
    public long TotalSizeBytes { get; set; }
    public string TargetTier { get; set; } = "Cool";
}
