using System.Text.Json;
using Arkive.Core.Interfaces;
using Arkive.Core.Models;
using Arkive.Data;
using Microsoft.Extensions.Logging;

namespace Arkive.Functions.Services;

public class AuditService : IAuditService
{
    private readonly ArkiveDbContext _db;
    private readonly ILogger<AuditService> _logger;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false,
    };

    public AuditService(ArkiveDbContext db, ILogger<AuditService> logger)
    {
        _db = db;
        _logger = logger;
    }

    public async Task LogAsync(AuditInput input, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(input);

        try
        {
            var entry = new AuditEntry
            {
                MspOrgId = input.MspOrgId,
                ClientTenantId = input.ClientTenantId,
                ActorId = input.ActorId,
                ActorName = input.ActorName,
                Action = input.Action,
                Details = input.Details is not null
                    ? JsonSerializer.Serialize(input.Details, JsonOptions)
                    : null,
                CorrelationId = input.CorrelationId,
                Timestamp = DateTimeOffset.UtcNow,
            };

            _db.AuditEntries.Add(entry);
            await _db.SaveChangesAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            // Audit logging should never break the calling operation
            _logger.LogError(ex,
                "Failed to write audit entry for action {Action} by {Actor} in org {OrgId}",
                input.Action, input.ActorName, input.MspOrgId);
        }
    }
}
