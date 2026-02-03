using Arkive.Core.Interfaces;
using Arkive.Data;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Microsoft.Azure.Functions.Worker;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Arkive.Functions.Orchestrators;

public class RehydrationActivities
{
    private readonly BlobServiceClient _blobClient;
    private readonly IArchiveService _archiveService;
    private readonly ArkiveDbContext _db;
    private readonly ILogger<RehydrationActivities> _logger;

    public RehydrationActivities(
        BlobServiceClient blobClient,
        IArchiveService archiveService,
        ArkiveDbContext db,
        ILogger<RehydrationActivities> logger)
    {
        _blobClient = blobClient;
        _archiveService = archiveService;
        _db = db;
        _logger = logger;
    }

    /// <summary>
    /// Initiates blob rehydration by setting the access tier from Archive to Cool.
    /// Returns true if rehydration was initiated, false if already accessible.
    /// </summary>
    [Function(nameof(InitiateRehydration))]
    public async Task<bool> InitiateRehydration(
        [ActivityTrigger] InitiateRehydrationInput input,
        CancellationToken cancellationToken)
    {
        var containerName = $"tenant-{input.TenantId}";
        var blobPath = input.FilePath.TrimStart('/');

        var containerClient = _blobClient.GetBlobContainerClient(containerName);
        var blobClient = containerClient.GetBlobClient(blobPath);

        var properties = await blobClient.GetPropertiesAsync(cancellationToken: cancellationToken);
        var currentTier = properties.Value.AccessTier;

        // If already in Cool or Hot, no rehydration needed
        if (currentTier == "Cool" || currentTier == "Hot")
        {
            _logger.LogInformation(
                "Blob {BlobPath} already in tier {Tier}, no rehydration needed",
                blobPath, currentTier);
            return false;
        }

        // Check if rehydration is already in progress
        var archiveStatus = properties.Value.ArchiveStatus;
        if (archiveStatus == "rehydrate-pending-to-cool")
        {
            _logger.LogInformation(
                "Blob {BlobPath} rehydration already in progress", blobPath);
            return true;
        }

        // Initiate rehydration by setting tier to Cool
        _logger.LogInformation(
            "Initiating rehydration for blob {BlobPath} from Archive to Cool", blobPath);

        await blobClient.SetAccessTierAsync(
            AccessTier.Cool,
            cancellationToken: cancellationToken);

        return true;
    }

    /// <summary>
    /// Checks if a blob has completed rehydration and is accessible.
    /// </summary>
    [Function(nameof(CheckRehydrationStatus))]
    public async Task<bool> CheckRehydrationStatus(
        [ActivityTrigger] CheckRehydrationInput input,
        CancellationToken cancellationToken)
    {
        var containerName = $"tenant-{input.TenantId}";
        var blobPath = input.FilePath.TrimStart('/');

        var containerClient = _blobClient.GetBlobContainerClient(containerName);
        var blobClient = containerClient.GetBlobClient(blobPath);

        var properties = await blobClient.GetPropertiesAsync(cancellationToken: cancellationToken);
        var currentTier = properties.Value.AccessTier;
        var archiveStatus = properties.Value.ArchiveStatus;

        var isReady = currentTier == "Cool" || currentTier == "Hot";

        _logger.LogInformation(
            "Rehydration status for {BlobPath}: Tier={Tier}, ArchiveStatus={ArchiveStatus}, Ready={Ready}",
            blobPath, currentTier, archiveStatus, isReady);

        return isReady;
    }

    /// <summary>
    /// Downloads the rehydrated blob and uploads it back to SharePoint via the archive service.
    /// </summary>
    [Function(nameof(RetrieveRehydratedFile))]
    public async Task<RehydrationRetrieveResult> RetrieveRehydratedFile(
        [ActivityTrigger] RehydrationInput input,
        CancellationToken cancellationToken)
    {
        _logger.LogInformation(
            "Retrieving rehydrated file {FileName} for tenant {TenantId}",
            input.FileName, input.TenantId);

        try
        {
            var result = await _archiveService.RetrieveFileAsync(new RetrieveFileInput
            {
                TenantId = input.TenantId,
                MspOrgId = input.MspOrgId,
                M365TenantId = input.M365TenantId,
                FileMetadataId = input.FileMetadataId,
                SiteId = input.SiteId,
                DriveId = input.DriveId,
                ItemId = input.ItemId,
                FileName = input.FileName,
                FilePath = input.FilePath,
                SizeBytes = input.SizeBytes,
                BlobTier = "Cool", // Now rehydrated to Cool
            }, cancellationToken);

            return new RehydrationRetrieveResult
            {
                Success = result.Status == "Completed",
                ErrorMessage = result.ErrorMessage,
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Failed to retrieve rehydrated file {FileName}: {Error}",
                input.FileName, ex.Message);

            return new RehydrationRetrieveResult
            {
                Success = false,
                ErrorMessage = ex.Message,
            };
        }
    }

    /// <summary>
    /// Updates an ArchiveOperation's status (used for failure tracking).
    /// </summary>
    [Function(nameof(UpdateOperationStatus))]
    public async Task UpdateOperationStatus(
        [ActivityTrigger] UpdateOperationStatusInput input,
        CancellationToken cancellationToken)
    {
        var operation = await _db.ArchiveOperations
            .Where(o => o.OperationId == input.OperationId && o.Action == "Retrieve")
            .FirstOrDefaultAsync(cancellationToken);

        if (operation is not null)
        {
            operation.Status = input.Status;
            operation.ErrorMessage = input.ErrorMessage;
            if (input.Status is "Completed" or "Failed")
                operation.CompletedAt = DateTimeOffset.UtcNow;
            await _db.SaveChangesAsync(cancellationToken);
        }
    }
}
