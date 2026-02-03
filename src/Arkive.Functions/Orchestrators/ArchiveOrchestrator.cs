using Arkive.Core.DTOs;
using Arkive.Core.Interfaces;
using Microsoft.Azure.Functions.Worker;
using Microsoft.DurableTask;
using Microsoft.Extensions.Logging;

namespace Arkive.Functions.Orchestrators;

public static class ArchiveOrchestrator
{
    [Function(nameof(ArchiveOrchestrator))]
    public static async Task<ArchiveOrchestrationResult> RunOrchestrator(
        [OrchestrationTrigger] TaskOrchestrationContext context)
    {
        var logger = context.CreateReplaySafeLogger(nameof(ArchiveOrchestrator));
        var input = context.GetInput<ArchiveOrchestrationInput>()
            ?? throw new InvalidOperationException("Archive orchestration input is required.");

        logger.LogInformation("Starting archive orchestration for tenant {TenantId}", input.TenantId);

        var result = new ArchiveOrchestrationResult
        {
            TenantId = input.TenantId,
            StartedAt = context.CurrentUtcDateTime,
        };

        try
        {
            // Step 1: Get files to archive
            var filesToArchive = await context.CallActivityAsync<List<ArchiveFileInput>>(
                nameof(ArchiveActivities.GetFilesToArchive),
                new GetFilesToArchiveInput
                {
                    TenantId = input.TenantId,
                    MspOrgId = input.MspOrgId,
                    RuleId = input.RuleId,
                });

            result.TotalFiles = filesToArchive.Count;

            if (filesToArchive.Count == 0)
            {
                logger.LogInformation("No files to archive for tenant {TenantId}", input.TenantId);
                result.Status = "Completed";
                result.CompletedAt = context.CurrentUtcDateTime;
                return result;
            }

            logger.LogInformation(
                "Found {FileCount} files to archive for tenant {TenantId}",
                filesToArchive.Count, input.TenantId);

            // Step 2: Send approval notifications to site owners
            var siteOwnerGroups = await context.CallActivityAsync<List<SiteOwnerFileGroup>>(
                nameof(NotificationActivities.GroupFilesBySiteOwner),
                new GroupFilesBySiteOwnerInput
                {
                    TenantId = input.TenantId,
                    MspOrgId = input.MspOrgId,
                    Files = filesToArchive,
                });

            // Track which sites are approved for archiving
            var approvedSiteIds = new HashSet<string>();
            var vetoedSiteIds = new HashSet<string>();
            var reviewSiteIds = new HashSet<string>();

            // Step 2a: Get tenant's auto-approval setting (before notifications)
            const int MaxWaitDaysWhenDisabled = 30;
            var autoApprovalDays = await context.CallActivityAsync<int?>(
                nameof(NotificationActivities.GetTenantAutoApprovalDays),
                new GetAutoApprovalDaysInput { TenantId = input.TenantId });

            if (autoApprovalDays == 0 && siteOwnerGroups.Count > 0)
            {
                // Immediate auto-approval — skip notifications entirely, auto-approve all sites
                logger.LogInformation(
                    "Tenant {TenantId} has immediate auto-approval (0 days) — skipping notifications, auto-approving all sites",
                    input.TenantId);

                // Set operations to AwaitingApproval first (consistent state transition)
                var allSiteIds = siteOwnerGroups.Select(g => g.SiteId).Distinct().ToList();
                await context.CallActivityAsync(
                    nameof(NotificationActivities.SetOperationsAwaitingApproval),
                    new SetAwaitingApprovalInput
                    {
                        TenantId = input.TenantId,
                        SiteIds = allSiteIds,
                    });

                // Auto-approve all sites in parallel (fan-out)
                var autoApproveTasks = new List<Task<int>>();
                foreach (var siteId in allSiteIds)
                {
                    autoApproveTasks.Add(
                        context.CallActivityAsync<int>(
                            nameof(NotificationActivities.AutoApproveExpiredOperations),
                            new AutoApproveExpiredInput
                            {
                                TenantId = input.TenantId,
                                SiteId = siteId,
                            }));
                }

                await Task.WhenAll(autoApproveTasks);

                foreach (var siteId in allSiteIds)
                {
                    approvedSiteIds.Add(siteId);
                    result.AutoApprovals++;
                }
            }
            else if (siteOwnerGroups.Count > 0)
            {
                // Step 2b: Send approval cards (fan-out)
                var notificationTasks = new List<Task<ApprovalNotificationResult>>();
                foreach (var group in siteOwnerGroups)
                {
                    notificationTasks.Add(
                        context.CallActivityAsync<ApprovalNotificationResult>(
                            nameof(NotificationActivities.SendApprovalCard),
                            new ApprovalNotificationInput
                            {
                                TenantId = input.TenantId,
                                MspOrgId = input.MspOrgId,
                                M365TenantId = input.M365TenantId,
                                SiteOwnerEmail = group.OwnerEmail,
                                SiteOwnerAadId = group.OwnerAadId,
                                SiteName = group.SiteName,
                                SiteId = group.SiteId,
                                FileCount = group.FileCount,
                                TotalSizeBytes = group.TotalSizeBytes,
                                TargetTier = group.TargetTier,
                                OrchestrationInstanceId = context.InstanceId,
                            }));
                }

                var notificationResults = await Task.WhenAll(notificationTasks);

                var delivered = notificationResults.Count(r => r.Delivered);
                var failed = notificationResults.Count(r => !r.Delivered);
                logger.LogInformation(
                    "Approval notifications: {Delivered} delivered, {Failed} failed for tenant {TenantId}",
                    delivered, failed, input.TenantId);

                result.NotificationsSent = delivered;
                result.NotificationsFailed = failed;

                // Step 2c: Set delivered site operations to AwaitingApproval
                var deliveredSiteIds = notificationResults
                    .Where(r => r.Delivered)
                    .Select(r => r.SiteId)
                    .Distinct()
                    .ToList();

                if (deliveredSiteIds.Count > 0)
                {
                    await context.CallActivityAsync(
                        nameof(NotificationActivities.SetOperationsAwaitingApproval),
                        new SetAwaitingApprovalInput
                        {
                            TenantId = input.TenantId,
                            SiteIds = deliveredSiteIds,
                        });
                }

                // Step 2d: Wait for approval events per site owner (configurable timeout)
                // null = auto-approval disabled (wait up to max orchestrator lifetime)
                // 1-365 = wait N days, then auto-approve on timeout
                var approvalTimeout = autoApprovalDays.HasValue
                    ? TimeSpan.FromDays(autoApprovalDays.Value)
                    : TimeSpan.FromDays(MaxWaitDaysWhenDisabled);

                var approvalTasks = new Dictionary<string, Task<ApprovalEventData>>();

                foreach (var notifResult in notificationResults)
                {
                    if (!notifResult.Delivered) continue;

                    var eventName = $"ApprovalReceived_{notifResult.SiteId}";
                    approvalTasks[notifResult.SiteId] = context.WaitForExternalEvent<ApprovalEventData>(
                        eventName, approvalTimeout);
                }

                // Wait for all approval events (or timeout).
                // Sequential await is intentional — all sites must respond (or timeout) before archiving proceeds.
                // Individual events are registered in parallel above; the await order doesn't affect total wait time.
                foreach (var (siteId, approvalTask) in approvalTasks)
                {
                    try
                    {
                        var eventData = await approvalTask;

                        switch (eventData.Action)
                        {
                            case "approve":
                                approvedSiteIds.Add(siteId);
                                result.ApprovalsReceived++;
                                logger.LogInformation(
                                    "Site {SiteId} approved by {Actor}", siteId, eventData.ActorEmail);
                                break;
                            case "reject":
                                vetoedSiteIds.Add(siteId);
                                result.VetoesReceived++;
                                logger.LogInformation(
                                    "Site {SiteId} vetoed by {Actor}. Reason: {Reason}",
                                    siteId, eventData.ActorEmail, eventData.Reason ?? "(none)");
                                break;
                            case "review":
                                reviewSiteIds.Add(siteId);
                                result.ReviewsRequested++;
                                logger.LogInformation(
                                    "Site {SiteId} review requested by {Actor}", siteId, eventData.ActorEmail);
                                break;
                        }
                    }
                    catch (TaskCanceledException)
                    {
                        if (autoApprovalDays.HasValue)
                        {
                            // Auto-approval enabled: timeout means auto-approve
                            var autoCount = await context.CallActivityAsync<int>(
                                nameof(NotificationActivities.AutoApproveExpiredOperations),
                                new AutoApproveExpiredInput
                                {
                                    TenantId = input.TenantId,
                                    SiteId = siteId,
                                });

                            approvedSiteIds.Add(siteId);
                            result.AutoApprovals++;
                            logger.LogInformation(
                                "Site {SiteId} auto-approved after {Days}-day timeout ({Count} operations)",
                                siteId, autoApprovalDays.Value, autoCount);
                        }
                        else
                        {
                            // Auto-approval disabled: timeout means skip
                            logger.LogWarning(
                                "Approval timeout for site {SiteId} — auto-approval disabled, skipping archive",
                                siteId);
                        }
                    }
                }

                // Sites where notification delivery failed are skipped
                var failedSiteIds = notificationResults
                    .Where(r => !r.Delivered)
                    .Select(r => r.SiteId)
                    .ToHashSet();

                logger.LogInformation(
                    "Approval results for tenant {TenantId}: {Approved} approved, {Vetoed} vetoed, {Review} review, {Failed} notification failures",
                    input.TenantId, approvedSiteIds.Count, vetoedSiteIds.Count, reviewSiteIds.Count, failedSiteIds.Count);
            }
            else
            {
                // No site owner groups — no notifications needed, all files proceed
                foreach (var file in filesToArchive)
                    approvedSiteIds.Add(file.SiteId);
            }

            // Step 3: Process approved files in batches (fan-out/fan-in, batch size 10)
            var approvedFiles = filesToArchive
                .Where(f => approvedSiteIds.Contains(f.SiteId))
                .ToList();

            var skippedFiles = filesToArchive.Count - approvedFiles.Count;
            if (skippedFiles > 0)
            {
                logger.LogInformation(
                    "Skipping {SkippedCount} files from vetoed/review/failed sites for tenant {TenantId}",
                    skippedFiles, input.TenantId);
            }

            result.SkippedFiles = skippedFiles;

            const int batchSize = 10;
            for (var i = 0; i < approvedFiles.Count; i += batchSize)
            {
                var batch = approvedFiles.Skip(i).Take(batchSize).ToList();

                var archiveTasks = new List<Task<ArchiveFileResult>>();
                foreach (var file in batch)
                {
                    archiveTasks.Add(
                        context.CallActivityAsync<ArchiveFileResult>(
                            nameof(ArchiveActivities.ArchiveSingleFile),
                            file,
                            new TaskOptions(new TaskRetryOptions(
                                new RetryPolicy(
                                    maxNumberOfAttempts: 3,
                                    firstRetryInterval: TimeSpan.FromSeconds(10))
                            ))));
                }

                var batchResults = await Task.WhenAll(archiveTasks);

                foreach (var batchResult in batchResults)
                {
                    if (batchResult.Success)
                        result.CompletedFiles++;
                    else
                        result.FailedFiles++;
                }

                logger.LogInformation(
                    "Archive batch {BatchIndex} completed: {Completed} succeeded, {Failed} failed",
                    i / batchSize + 1, result.CompletedFiles, result.FailedFiles);
            }

            result.Status = result.FailedFiles > 0 ? "CompletedWithErrors" : "Completed";
            result.CompletedAt = context.CurrentUtcDateTime;

            logger.LogInformation(
                "Archive orchestration completed for tenant {TenantId}: {Completed}/{Total} files, {Failed} failures",
                input.TenantId, result.CompletedFiles, result.TotalFiles, result.FailedFiles);
        }
        catch (TaskCanceledException)
        {
            throw;
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            logger.LogCritical(ex,
                "Archive orchestration failed for tenant {TenantId}: {ErrorMessage}",
                input.TenantId, ex.Message);

            result.Status = "Failed";
            result.ErrorMessage = ex.Message;
            result.CompletedAt = context.CurrentUtcDateTime;
        }

        return result;
    }
}

public class ArchiveOrchestrationInput
{
    public Guid TenantId { get; set; }
    public Guid MspOrgId { get; set; }
    public string M365TenantId { get; set; } = string.Empty;
    public Guid? RuleId { get; set; }
}

public class ArchiveOrchestrationResult
{
    public Guid TenantId { get; set; }
    public string Status { get; set; } = "InProgress";
    public int TotalFiles { get; set; }
    public int CompletedFiles { get; set; }
    public int FailedFiles { get; set; }
    public int SkippedFiles { get; set; }
    public int NotificationsSent { get; set; }
    public int NotificationsFailed { get; set; }
    public int ApprovalsReceived { get; set; }
    public int VetoesReceived { get; set; }
    public int ReviewsRequested { get; set; }
    public int AutoApprovals { get; set; }
    public string? ErrorMessage { get; set; }
    public DateTimeOffset StartedAt { get; set; }
    public DateTimeOffset? CompletedAt { get; set; }
}

public class GetFilesToArchiveInput
{
    public Guid TenantId { get; set; }
    public Guid MspOrgId { get; set; }
    public Guid? RuleId { get; set; }
}

public class ArchiveFileResult
{
    public Guid FileMetadataId { get; set; }
    public bool Success { get; set; }
    public string? ErrorMessage { get; set; }
}
