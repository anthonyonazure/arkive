namespace Arkive.Core.Interfaces;

public interface IAuditLogService
{
    Task<AuditLogResult> GetLastAccessedDatesAsync(string m365TenantId, Guid clientTenantId, CancellationToken cancellationToken = default);
    Task UpdateAuditLogAvailabilityAsync(Guid clientTenantId, bool available, CancellationToken cancellationToken = default);
}

public class AuditLogResult
{
    public int FilesUpdated { get; set; }
    public bool AuditLogAvailable { get; set; }
    public bool FallbackApplied { get; set; }
}
