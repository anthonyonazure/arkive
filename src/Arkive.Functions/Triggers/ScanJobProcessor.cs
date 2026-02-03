using System.Text.Json;
using Arkive.Functions.Orchestrators;
using Microsoft.Azure.Functions.Worker;
using Microsoft.DurableTask;
using Microsoft.DurableTask.Client;
using Microsoft.Extensions.Logging;

namespace Arkive.Functions.Triggers;

public class ScanJobProcessor
{
    private readonly ILogger<ScanJobProcessor> _logger;

    public ScanJobProcessor(ILogger<ScanJobProcessor> logger)
    {
        _logger = logger;
    }

    [Function(nameof(ScanJobProcessor))]
    public async Task Run(
        [ServiceBusTrigger("scan-jobs", Connection = "ServiceBusConnection")] string messageBody,
        [DurableClient] DurableTaskClient durableClient,
        CancellationToken cancellationToken)
    {
        var scanJob = JsonSerializer.Deserialize<ScanJobMessage>(messageBody);
        if (scanJob is null)
        {
            _logger.LogError("Failed to deserialize scan job message: {MessageBody}", messageBody);
            return; // Complete the message (don't retry for bad messages)
        }

        _logger.LogInformation(
            "Processing scan job for tenant {TenantId}, scheduled at {ScheduledAt}",
            scanJob.TenantId, scanJob.ScheduledAt);

        // Use deterministic instance ID to prevent duplicate scans (same pattern as ScanEndpoints)
        var instanceId = $"scan-{scanJob.TenantId}";

        // Check for existing running scan
        var existingInstance = await durableClient.GetInstanceAsync(instanceId, cancellation: cancellationToken);
        if (existingInstance is not null &&
            existingInstance.RuntimeStatus is OrchestrationRuntimeStatus.Running or OrchestrationRuntimeStatus.Pending)
        {
            _logger.LogWarning(
                "Scan already running for tenant {TenantId}, instance {InstanceId}, skipping",
                scanJob.TenantId, instanceId);
            return; // Complete the message — don't requeue, scan is already in progress
        }

        var input = new ScanOrchestrationInput
        {
            TenantId = scanJob.TenantId,
            MspOrgId = scanJob.MspOrgId,
            M365TenantId = scanJob.M365TenantId
        };

        await durableClient.ScheduleNewOrchestrationInstanceAsync(
            nameof(TenantScanOrchestrator),
            input,
            new StartOrchestrationOptions { InstanceId = instanceId },
            cancellation: cancellationToken);

        _logger.LogInformation(
            "Started scan orchestration {InstanceId} for tenant {TenantId} via scheduled scan",
            instanceId, scanJob.TenantId);
    }
}
