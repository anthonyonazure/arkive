namespace Arkive.Core.DTOs;

public class AuditEntryDto
{
    public Guid Id { get; set; }
    public Guid MspOrgId { get; set; }
    public Guid? ClientTenantId { get; set; }
    public string? TenantName { get; set; }
    public string ActorId { get; set; } = string.Empty;
    public string ActorName { get; set; } = string.Empty;
    public string Action { get; set; } = string.Empty;
    public string? Details { get; set; }
    public string? CorrelationId { get; set; }
    public DateTimeOffset Timestamp { get; set; }
}

public class AuditSearchResult
{
    public List<AuditEntryDto> Entries { get; set; } = [];
    public int Page { get; set; }
    public int PageSize { get; set; }
    public int TotalCount { get; set; }
    public int TotalPages { get; set; }
}
