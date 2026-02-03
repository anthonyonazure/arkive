namespace Arkive.Core.Models;

public class AuditEntry
{
    public Guid Id { get; set; }
    public Guid MspOrgId { get; set; }
    public Guid? ClientTenantId { get; set; }
    public string ActorId { get; set; } = string.Empty;
    public string ActorName { get; set; } = string.Empty;
    public string Action { get; set; } = string.Empty;
    public string? Details { get; set; }
    public string? CorrelationId { get; set; }
    public DateTimeOffset Timestamp { get; set; }

    // Navigation properties
    public MspOrganization MspOrganization { get; set; } = null!;
    public ClientTenant? ClientTenant { get; set; }
}
