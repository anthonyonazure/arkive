using Microsoft.Azure.Functions.Worker;
using Microsoft.DurableTask;
using Microsoft.Extensions.Logging;

namespace Arkive.Functions.Orchestrators;

public static class TenantScanOrchestrator
{
    [Function(nameof(TenantScanOrchestrator))]
    public static async Task<ScanResultSummary> RunOrchestrator(
        [OrchestrationTrigger] TaskOrchestrationContext context)
    {
        var logger = context.CreateReplaySafeLogger(nameof(TenantScanOrchestrator));
        var input = context.GetInput<ScanOrchestrationInput>()
            ?? throw new InvalidOperationException("Scan orchestration input is required.");

        logger.LogInformation("Starting scan orchestration for tenant {TenantId}", input.TenantId);

        ScanResultSummary summary;

        try
        {
            // Step 1: Get selected sites for this tenant
            var sites = await context.CallActivityAsync<List<SelectedSiteInfo>>(
                nameof(GraphApiActivities.GetSelectedSites),
                input.TenantId);

            logger.LogInformation("Found {SiteCount} selected sites for tenant {TenantId}", sites.Count, input.TenantId);

            if (sites.Count == 0)
            {
                logger.LogWarning("No selected sites found for tenant {TenantId}, nothing to scan", input.TenantId);
                summary = new ScanResultSummary { SitesScanned = 0, TotalFiles = 0, TotalBytes = 0 };

                await context.CallActivityAsync(
                    nameof(ScanCompletionActivities.PublishScanCompleted),
                    new ScanCompletedEvent
                    {
                        TenantId = input.TenantId,
                        Summary = summary,
                        CompletedAt = context.CurrentUtcDateTime,
                        Success = true
                    });

                return summary;
            }

            // Step 2: Fan-out — enumerate files for each site in parallel
            var enumerationTasks = new List<Task<SiteFilesResult>>();
            foreach (var site in sites)
            {
                var siteInput = new SiteEnumerationInput
                {
                    M365TenantId = input.M365TenantId,
                    SiteId = site.SiteId
                };
                enumerationTasks.Add(
                    context.CallActivityAsync<SiteFilesResult>(
                        nameof(GraphApiActivities.EnumerateFilesForSite),
                        siteInput));
            }

            // Step 3: Fan-in — collect all results
            var siteResults = await Task.WhenAll(enumerationTasks);

            // Step 4: Save file metadata in batches per site
            var totalFiles = 0;
            long totalBytes = 0;

            foreach (var siteResult in siteResults)
            {
                if (siteResult.Files.Count == 0) continue;

                var saveInput = new SaveFileMetadataInput
                {
                    ClientTenantId = input.TenantId,
                    MspOrgId = input.MspOrgId,
                    Files = siteResult.Files
                };

                var upsertedCount = await context.CallActivityAsync<int>(
                    nameof(GraphApiActivities.SaveFileMetadataBatch),
                    saveInput);

                totalFiles += upsertedCount;
                totalBytes += siteResult.Files.Sum(f => f.SizeBytes);
            }

            // Step 5: Index audit log for file access dates (LastAccessedAt)
            var auditLogInput = new AuditLogInput
            {
                M365TenantId = input.M365TenantId,
                ClientTenantId = input.TenantId
            };

            var auditLogResult = await context.CallActivityAsync<AuditLogActivityResult>(
                nameof(AuditLogActivities.GetLastAccessedDates),
                auditLogInput);

            logger.LogInformation(
                "Audit log indexing for tenant {TenantId}: {FilesUpdated} files updated, AuditLogAvailable={AuditLogAvailable}",
                input.TenantId, auditLogResult.FilesUpdated, auditLogResult.AuditLogAvailable);

            // Step 6: Update scan timestamp
            await context.CallActivityAsync(
                nameof(GraphApiActivities.UpdateScanTimestamp),
                input.TenantId);

            summary = new ScanResultSummary
            {
                SitesScanned = sites.Count,
                TotalFiles = totalFiles,
                TotalBytes = totalBytes,
                AuditLogFilesUpdated = auditLogResult.FilesUpdated,
                AuditLogAvailable = auditLogResult.AuditLogAvailable
            };

            logger.LogInformation(
                "Scan completed for tenant {TenantId}: {SitesScanned} sites, {TotalFiles} files, {TotalBytes} bytes, {AuditLogFiles} audit log updates",
                input.TenantId, summary.SitesScanned, summary.TotalFiles, summary.TotalBytes, summary.AuditLogFilesUpdated);

            // Step 7: Publish scan completed event
            await context.CallActivityAsync(
                nameof(ScanCompletionActivities.PublishScanCompleted),
                new ScanCompletedEvent
                {
                    TenantId = input.TenantId,
                    Summary = summary,
                    CompletedAt = context.CurrentUtcDateTime,
                    Success = true
                });
        }
        catch (TaskCanceledException)
        {
            // Don't treat cancellation/shutdown as a scan failure — let Durable Functions handle it
            throw;
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            logger.LogCritical(ex,
                "Scan orchestration failed for tenant {TenantId}: {ErrorMessage}",
                input.TenantId, ex.Message);

            // Publish scan failure event (NFR19 — Application Insights alerting)
            summary = new ScanResultSummary();
            try
            {
                await context.CallActivityAsync(
                    nameof(ScanCompletionActivities.PublishScanFailed),
                    new ScanCompletedEvent
                    {
                        TenantId = input.TenantId,
                        Summary = summary,
                        CompletedAt = context.CurrentUtcDateTime,
                        Success = false,
                        ErrorMessage = ex.Message
                    });
            }
            catch (Exception publishEx)
            {
                logger.LogError(publishEx,
                    "Failed to publish scan failure event for tenant {TenantId}", input.TenantId);
            }

            throw; // Re-throw so Durable Functions marks the orchestration as failed
        }

        return summary;
    }
}

public class SelectedSiteInfo
{
    public string SiteId { get; set; } = string.Empty;
}

public class SiteFilesResult
{
    public string SiteId { get; set; } = string.Empty;
    public List<FileMetadataBatchItem> Files { get; set; } = [];
}
