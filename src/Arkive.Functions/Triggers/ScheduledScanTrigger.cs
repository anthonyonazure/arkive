using System.Text.Json;
using Arkive.Core.Enums;
using Arkive.Data;
using Arkive.Functions.Orchestrators;
using Azure.Messaging.ServiceBus;
using Microsoft.Azure.Functions.Worker;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Arkive.Functions.Triggers;

public class ScheduledScanTrigger
{
    private readonly ArkiveDbContext _db;
    private readonly ServiceBusClient? _serviceBusClient;
    private readonly ILogger<ScheduledScanTrigger> _logger;

    private const string ScanJobsQueue = "scan-jobs";
    private const int TargetLocalHour = 2; // 2 AM in tenant's local timezone
    private const int MinHoursSinceLastScan = 20;

    public ScheduledScanTrigger(
        ArkiveDbContext db,
        ILogger<ScheduledScanTrigger> logger,
        ServiceBusClient? serviceBusClient = null)
    {
        _db = db;
        _serviceBusClient = serviceBusClient;
        _logger = logger;
    }

    [Function(nameof(ScheduledScanTrigger))]
    public async Task Run(
        [TimerTrigger("0 0 * * * *")] TimerInfo timerInfo,
        CancellationToken cancellationToken)
    {
        _logger.LogInformation("Scheduled scan trigger fired at {UtcNow}", DateTimeOffset.UtcNow);

        var connectedTenants = await _db.ClientTenants
            .AsNoTracking()
            .Where(t => t.Status == TenantStatus.Connected)
            .Select(t => new
            {
                t.Id,
                t.MspOrgId,
                t.M365TenantId,
                t.ScanScheduleTimezone,
                t.LastScannedAt
            })
            .ToListAsync(cancellationToken);

        if (connectedTenants.Count == 0)
        {
            _logger.LogInformation("No connected tenants found, skipping scheduled scan");
            return;
        }

        var now = DateTimeOffset.UtcNow;
        var eligibleTenants = connectedTenants.Where(t =>
        {
            // Calculate local time for this tenant
            var timezone = ResolveTimezone(t.ScanScheduleTimezone);
            var localTime = TimeZoneInfo.ConvertTime(now, timezone);

            // Check if it's the target hour (2 AM local)
            if (localTime.Hour != TargetLocalHour)
                return false;

            // Skip if scanned recently (within MinHoursSinceLastScan)
            if (t.LastScannedAt.HasValue &&
                (now - t.LastScannedAt.Value).TotalHours < MinHoursSinceLastScan)
                return false;

            return true;
        }).ToList();

        if (eligibleTenants.Count == 0)
        {
            _logger.LogInformation("No tenants eligible for scanning at this time");
            return;
        }

        _logger.LogInformation("Found {EligibleCount} tenants eligible for overnight scan", eligibleTenants.Count);

        if (_serviceBusClient is null)
        {
            _logger.LogWarning(
                "ServiceBusClient not configured — {Count} eligible tenants will not be scanned. " +
                "Set the ServiceBusConnection setting to enable scheduled scans.",
                eligibleTenants.Count);
            return;
        }

        await using var sender = _serviceBusClient.CreateSender(ScanJobsQueue);

        var batch = await sender.CreateMessageBatchAsync(cancellationToken);

        foreach (var tenant in eligibleTenants)
        {
            var message = new ScanJobMessage
            {
                TenantId = tenant.Id,
                MspOrgId = tenant.MspOrgId,
                M365TenantId = tenant.M365TenantId,
                ScheduledAt = now
            };

            var sbMessage = new ServiceBusMessage(JsonSerializer.Serialize(message))
            {
                ContentType = "application/json",
                Subject = "scheduled-scan",
                MessageId = $"scan-{tenant.Id}-{now:yyyyMMddHH}"
            };

            if (!batch.TryAddMessage(sbMessage))
            {
                // Batch full — send current batch and start a new one
                await sender.SendMessagesAsync(batch, cancellationToken);
                batch.Dispose();
                batch = await sender.CreateMessageBatchAsync(cancellationToken);

                if (!batch.TryAddMessage(sbMessage))
                {
                    _logger.LogError("Scan job message for tenant {TenantId} is too large for Service Bus", tenant.Id);
                    continue;
                }
            }

            _logger.LogInformation(
                "Queued scan job for tenant {TenantId} (timezone: {Timezone})",
                tenant.Id, tenant.ScanScheduleTimezone ?? "UTC");
        }

        // Send any remaining messages in the batch
        if (batch.Count > 0)
            await sender.SendMessagesAsync(batch, cancellationToken);

        batch.Dispose();

        _logger.LogInformation("Published {Count} scan jobs to {Queue}", eligibleTenants.Count, ScanJobsQueue);
    }

    private static TimeZoneInfo ResolveTimezone(string? timezoneId)
    {
        if (string.IsNullOrEmpty(timezoneId))
            return TimeZoneInfo.Utc;

        try
        {
            return TimeZoneInfo.FindSystemTimeZoneById(timezoneId);
        }
        catch (TimeZoneNotFoundException)
        {
            // Fall back to UTC if timezone ID is invalid
            return TimeZoneInfo.Utc;
        }
    }
}
