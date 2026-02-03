namespace Arkive.Core.DTOs;

/// <summary>
/// Input received from a Teams Adaptive Card action (approve/reject/review).
/// </summary>
public class ApprovalActionInput
{
    public string Action { get; set; } = string.Empty;
    public string TenantId { get; set; } = string.Empty;
    public string MspOrgId { get; set; } = string.Empty;
    public string SiteId { get; set; } = string.Empty;
    public string OrchestrationInstanceId { get; set; } = string.Empty;
    public string? Reason { get; set; }
    public string ActorEmail { get; set; } = string.Empty;
    public string ActorName { get; set; } = string.Empty;
}

/// <summary>
/// Result of processing an approval action.
/// </summary>
public class ApprovalActionResult
{
    public bool Success { get; set; }
    public string Action { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
}

/// <summary>
/// Event data sent to the orchestrator via RaiseEvent when an approval action is received.
/// </summary>
public class ApprovalEventData
{
    public string Action { get; set; } = string.Empty;
    public string SiteId { get; set; } = string.Empty;
    public string ActorEmail { get; set; } = string.Empty;
    public string? Reason { get; set; }
    public DateTimeOffset Timestamp { get; set; }
}
