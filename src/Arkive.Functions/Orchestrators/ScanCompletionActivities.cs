using System.Text.Json;
using Azure.Messaging.ServiceBus;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;

namespace Arkive.Functions.Orchestrators;

public class ScanCompletionActivities
{
    private readonly ServiceBusClient? _serviceBusClient;
    private readonly ILogger<ScanCompletionActivities> _logger;

    private const string ScanCompletedQueue = "scan-completed";

    public ScanCompletionActivities(
        ILogger<ScanCompletionActivities> logger,
        ServiceBusClient? serviceBusClient = null)
    {
        _serviceBusClient = serviceBusClient;
        _logger = logger;
    }

    [Function(nameof(PublishScanCompleted))]
    public async Task PublishScanCompleted(
        [ActivityTrigger] ScanCompletedEvent completedEvent,
        CancellationToken cancellationToken)
    {
        _logger.LogInformation(
            "TenantScanCompleted: TenantId={TenantId}, Sites={SitesScanned}, Files={TotalFiles}, Bytes={TotalBytes}, AuditLogUpdated={AuditLogFiles}, Success={Success}",
            completedEvent.TenantId,
            completedEvent.Summary.SitesScanned,
            completedEvent.Summary.TotalFiles,
            completedEvent.Summary.TotalBytes,
            completedEvent.Summary.AuditLogFilesUpdated,
            completedEvent.Success);

        if (_serviceBusClient is not null)
        {
            await using var sender = _serviceBusClient.CreateSender(ScanCompletedQueue);
            var message = new ServiceBusMessage(JsonSerializer.Serialize(completedEvent))
            {
                ContentType = "application/json",
                Subject = "scan-completed",
                MessageId = $"scan-completed-{completedEvent.TenantId}-{completedEvent.CompletedAt:yyyyMMddHHmmss}"
            };
            await sender.SendMessageAsync(message, cancellationToken);
        }
    }

    [Function(nameof(PublishScanFailed))]
    public async Task PublishScanFailed(
        [ActivityTrigger] ScanCompletedEvent failedEvent,
        CancellationToken cancellationToken)
    {
        // Log scan failure as a critical event for Application Insights alerting (NFR19)
        _logger.LogCritical(
            "TenantScanFailed: TenantId={TenantId}, Error={ErrorMessage}. " +
            "Scan failed after retries. Check dead-letter queue and Application Insights for details.",
            failedEvent.TenantId,
            failedEvent.ErrorMessage);

        if (_serviceBusClient is not null)
        {
            await using var sender = _serviceBusClient.CreateSender(ScanCompletedQueue);
            var message = new ServiceBusMessage(JsonSerializer.Serialize(failedEvent))
            {
                ContentType = "application/json",
                Subject = "scan-failed",
                MessageId = $"scan-failed-{failedEvent.TenantId}-{failedEvent.CompletedAt:yyyyMMddHHmmss}"
            };
            await sender.SendMessageAsync(message, cancellationToken);
        }
    }
}
