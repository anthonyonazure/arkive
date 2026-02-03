using Arkive.Core.Interfaces;
using Microsoft.Azure.Functions.Worker;
using Microsoft.DurableTask;
using Microsoft.Extensions.Logging;

namespace Arkive.Functions.Orchestrators;

public static class RehydrationOrchestrator
{
    [Function(nameof(RehydrationOrchestrator))]
    public static async Task<RehydrationResult> RunOrchestrator(
        [OrchestrationTrigger] TaskOrchestrationContext context)
    {
        var logger = context.CreateReplaySafeLogger(nameof(RehydrationOrchestrator));
        var input = context.GetInput<RehydrationInput>()
            ?? throw new InvalidOperationException("Rehydration input is required.");

        logger.LogInformation(
            "Starting rehydration orchestration for file {FileName} ({FileId})",
            input.FileName, input.FileMetadataId);

        var result = new RehydrationResult
        {
            FileMetadataId = input.FileMetadataId,
            FileName = input.FileName,
            StartedAt = context.CurrentUtcDateTime,
        };

        const int maxRetries = 3;
        var attempt = 0;

        while (attempt < maxRetries)
        {
            attempt++;

            try
            {
                // Step 1: Initiate rehydration (set tier from Archive to Cool)
                var rehydrationStarted = await context.CallActivityAsync<bool>(
                    nameof(RehydrationActivities.InitiateRehydration),
                    new InitiateRehydrationInput
                    {
                        TenantId = input.TenantId,
                        FilePath = input.FilePath,
                    });

                if (!rehydrationStarted)
                {
                    // Blob already in Cool/Hot — proceed directly to retrieval
                    logger.LogInformation(
                        "File {FileName} already rehydrated, proceeding to retrieval", input.FileName);
                }
                else
                {
                    // Step 2: Poll rehydration status every 30 minutes (archive rehydration takes 4-15 hours)
                    var maxPollTime = context.CurrentUtcDateTime.AddHours(16);
                    var pollInterval = TimeSpan.FromMinutes(30);

                    while (context.CurrentUtcDateTime < maxPollTime)
                    {
                        var isReady = await context.CallActivityAsync<bool>(
                            nameof(RehydrationActivities.CheckRehydrationStatus),
                            new CheckRehydrationInput
                            {
                                TenantId = input.TenantId,
                                FilePath = input.FilePath,
                            });

                        if (isReady)
                        {
                            logger.LogInformation(
                                "Rehydration complete for file {FileName}", input.FileName);
                            break;
                        }

                        logger.LogInformation(
                            "Rehydration in progress for file {FileName}, next poll in {Interval} minutes",
                            input.FileName, pollInterval.TotalMinutes);

                        // Durable timer — does NOT consume resources while waiting
                        await context.CreateTimer(
                            context.CurrentUtcDateTime.Add(pollInterval),
                            CancellationToken.None);
                    }

                    // Final check after max poll time
                    var finalCheck = await context.CallActivityAsync<bool>(
                        nameof(RehydrationActivities.CheckRehydrationStatus),
                        new CheckRehydrationInput
                        {
                            TenantId = input.TenantId,
                            FilePath = input.FilePath,
                        });

                    if (!finalCheck)
                    {
                        throw new InvalidOperationException(
                            $"Rehydration timed out after 16 hours for {input.FileName}");
                    }
                }

                // Step 3: Update status to Retrieving, then retrieve the file
                await context.CallActivityAsync(
                    nameof(RehydrationActivities.UpdateOperationStatus),
                    new UpdateOperationStatusInput
                    {
                        OperationId = input.OperationId,
                        Status = "Retrieving",
                    });

                var retrieveResult = await context.CallActivityAsync<RehydrationRetrieveResult>(
                    nameof(RehydrationActivities.RetrieveRehydratedFile),
                    input);

                if (!retrieveResult.Success)
                    throw new InvalidOperationException(
                        $"Retrieval failed for {input.FileName}: {retrieveResult.ErrorMessage}");

                result.Status = "Completed";
                result.CompletedAt = context.CurrentUtcDateTime;
                logger.LogInformation(
                    "Rehydration + retrieval completed for file {FileName}", input.FileName);
                return result;
            }
            catch (TaskCanceledException)
            {
                throw;
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex) when (attempt < maxRetries)
            {
                logger.LogWarning(ex,
                    "Rehydration attempt {Attempt}/{MaxRetries} failed for file {FileName}: {Error}",
                    attempt, maxRetries, input.FileName, ex.Message);

                // Wait before retry (exponential backoff: 5 min, 15 min, 45 min)
                var backoff = TimeSpan.FromMinutes(5 * Math.Pow(3, attempt - 1));
                await context.CreateTimer(
                    context.CurrentUtcDateTime.Add(backoff),
                    CancellationToken.None);
            }
            catch (Exception ex)
            {
                logger.LogError(ex,
                    "Rehydration failed after {MaxRetries} attempts for file {FileName}: {Error}",
                    maxRetries, input.FileName, ex.Message);

                result.Status = "Failed";
                result.ErrorMessage = ex.Message;
                result.CompletedAt = context.CurrentUtcDateTime;

                // Update operation status to Failed
                await context.CallActivityAsync(
                    nameof(RehydrationActivities.UpdateOperationStatus),
                    new UpdateOperationStatusInput
                    {
                        OperationId = input.OperationId,
                        Status = "Failed",
                        ErrorMessage = ex.Message,
                    });

                return result;
            }
        }

        return result;
    }
}

public class RehydrationInput
{
    public Guid TenantId { get; set; }
    public Guid MspOrgId { get; set; }
    public string M365TenantId { get; set; } = string.Empty;
    public Guid FileMetadataId { get; set; }
    public string SiteId { get; set; } = string.Empty;
    public string DriveId { get; set; } = string.Empty;
    public string ItemId { get; set; } = string.Empty;
    public string FileName { get; set; } = string.Empty;
    public string FilePath { get; set; } = string.Empty;
    public long SizeBytes { get; set; }
    public string OperationId { get; set; } = string.Empty;
}

public class RehydrationResult
{
    public Guid FileMetadataId { get; set; }
    public string FileName { get; set; } = string.Empty;
    public string Status { get; set; } = "InProgress";
    public string? ErrorMessage { get; set; }
    public DateTimeOffset StartedAt { get; set; }
    public DateTimeOffset? CompletedAt { get; set; }
}

public class InitiateRehydrationInput
{
    public Guid TenantId { get; set; }
    public string FilePath { get; set; } = string.Empty;
}

public class CheckRehydrationInput
{
    public Guid TenantId { get; set; }
    public string FilePath { get; set; } = string.Empty;
}

public class RehydrationRetrieveResult
{
    public bool Success { get; set; }
    public string? ErrorMessage { get; set; }
}

public class UpdateOperationStatusInput
{
    public string OperationId { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string? ErrorMessage { get; set; }
}
