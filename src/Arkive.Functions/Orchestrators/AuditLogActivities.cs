using Arkive.Core.Interfaces;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;

namespace Arkive.Functions.Orchestrators;

public class AuditLogActivities
{
    private readonly IAuditLogService _auditLogService;
    private readonly ILogger<AuditLogActivities> _logger;

    public AuditLogActivities(IAuditLogService auditLogService, ILogger<AuditLogActivities> logger)
    {
        _auditLogService = auditLogService;
        _logger = logger;
    }

    [Function(nameof(GetLastAccessedDates))]
    public async Task<AuditLogActivityResult> GetLastAccessedDates(
        [ActivityTrigger] AuditLogInput input,
        CancellationToken cancellationToken)
    {
        _logger.LogInformation("Starting audit log indexing for tenant {TenantId} (M365: {M365TenantId})",
            input.ClientTenantId, input.M365TenantId);

        var result = await _auditLogService.GetLastAccessedDatesAsync(
            input.M365TenantId, input.ClientTenantId, cancellationToken);

        _logger.LogInformation(
            "Audit log indexing complete for tenant {TenantId}: {FilesUpdated} files updated, AuditLogAvailable={AuditLogAvailable}, FallbackApplied={FallbackApplied}",
            input.ClientTenantId, result.FilesUpdated, result.AuditLogAvailable, result.FallbackApplied);

        return new AuditLogActivityResult
        {
            FilesUpdated = result.FilesUpdated,
            AuditLogAvailable = result.AuditLogAvailable,
            FallbackApplied = result.FallbackApplied
        };
    }
}
