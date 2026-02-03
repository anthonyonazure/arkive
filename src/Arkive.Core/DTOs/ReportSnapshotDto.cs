namespace Arkive.Core.DTOs;

/// <summary>
/// Request body for creating a report snapshot (shareable link).
/// </summary>
public class CreateReportSnapshotRequest
{
    public Guid TenantId { get; set; }
}

/// <summary>
/// Response after creating a report snapshot.
/// </summary>
public class ReportSnapshotResponse
{
    public string Token { get; set; } = string.Empty;
    public string Url { get; set; } = string.Empty;
    public DateTimeOffset ExpiresAt { get; set; }
}

/// <summary>
/// Public report data returned when accessing a shared link.
/// </summary>
public class SharedReportData
{
    public string TenantName { get; set; } = string.Empty;
    public DateTimeOffset GeneratedAt { get; set; }
    public DateTimeOffset ExpiresAt { get; set; }
    public object? Analytics { get; set; }
    public object? Trends { get; set; }
}
