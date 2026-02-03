namespace Arkive.Core.DTOs;

/// <summary>
/// A vetoed archive operation displayed in the veto review dashboard.
/// </summary>
public class VetoReviewDto
{
    public Guid OperationId { get; set; }
    public Guid FileMetadataId { get; set; }
    public string FileName { get; set; } = string.Empty;
    public string FilePath { get; set; } = string.Empty;
    public string SiteId { get; set; } = string.Empty;
    public string SiteName { get; set; } = string.Empty;
    public long SizeBytes { get; set; }
    public string VetoedBy { get; set; } = string.Empty;
    public string? VetoReason { get; set; }
    public DateTimeOffset? VetoedAt { get; set; }
}

/// <summary>
/// Request to resolve a vetoed operation.
/// </summary>
public class VetoActionRequest
{
    /// <summary>
    /// Action to take: "accept" (keep file), "override" (re-queue for archive), "exclude" (create exclusion rule).
    /// </summary>
    public string Action { get; set; } = string.Empty;
}

/// <summary>
/// Result of a veto resolution action.
/// </summary>
public class VetoActionResult
{
    public bool Success { get; set; }
    public string Action { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public Guid? ExclusionRuleId { get; set; }
}
