namespace Arkive.Core.Interfaces;

/// <summary>
/// Compliance audit trail service. Call LogAsync AFTER the calling operation's SaveChangesAsync
/// to avoid flushing unrelated pending changes (shares the same DbContext).
/// </summary>
public interface IAuditService
{
    Task LogAsync(AuditInput input, CancellationToken cancellationToken = default);
}

public class AuditInput
{
    public Guid MspOrgId { get; set; }
    public Guid? ClientTenantId { get; set; }
    public string ActorId { get; set; } = "system";
    public string ActorName { get; set; } = "System";
    public string Action { get; set; } = string.Empty;
    public object? Details { get; set; }
    public string? CorrelationId { get; set; }
}
