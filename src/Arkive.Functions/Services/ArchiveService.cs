using System.Security.Cryptography;
using System.Text;
using Arkive.Core.Configuration;
using Arkive.Core.DTOs;
using Arkive.Core.Interfaces;
using Arkive.Core.Models;
using Arkive.Data;
using Azure.Identity;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.Graph;

namespace Arkive.Functions.Services;

public class ArchiveService : IArchiveService
{
    private readonly ArkiveDbContext _db;
    private readonly BlobServiceClient _blobClient;
    private readonly EntraIdOptions _entraIdOptions;
    private readonly IKeyVaultService _keyVaultService;
    private readonly IRuleEvaluationService _evaluationService;
    private readonly IAuditService _auditService;
    private readonly ILogger<ArchiveService> _logger;

    private const string ClientSecretName = "arkive-client-secret";

    public ArchiveService(
        ArkiveDbContext db,
        BlobServiceClient blobClient,
        IOptions<EntraIdOptions> entraIdOptions,
        IKeyVaultService keyVaultService,
        IRuleEvaluationService evaluationService,
        IAuditService auditService,
        ILogger<ArchiveService> logger)
    {
        _db = db;
        _blobClient = blobClient;
        _entraIdOptions = entraIdOptions.Value;
        _keyVaultService = keyVaultService;
        _evaluationService = evaluationService;
        _auditService = auditService;
        _logger = logger;
    }

    public async Task<ArchiveOperationDto> ArchiveFileAsync(
        ArchiveFileInput input,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(input);

        // Generate deterministic operation ID for idempotency
        var operationId = GenerateOperationId(input.FileMetadataId, input.RuleId);

        // Check if this operation already exists (idempotency check)
        // Allow re-archiving if the previous operation completed or failed (e.g., after a restore)
        var existing = await _db.ArchiveOperations
            .AsNoTracking()
            .FirstOrDefaultAsync(o => o.OperationId == operationId, cancellationToken);

        if (existing is not null && existing.Status == "InProgress")
        {
            _logger.LogInformation("Archive operation {OperationId} already in progress", operationId);
            return MapToDto(existing);
        }

        if (existing is not null && existing.Status is "Completed" or "Failed")
        {
            // Remove the old operation record so a new one can be created with the same OperationId
            _db.ArchiveOperations.Remove(await _db.ArchiveOperations.FirstAsync(o => o.OperationId == operationId, cancellationToken));
            await _db.SaveChangesAsync(cancellationToken);
        }

        var containerName = $"tenant-{input.TenantId}";
        var blobPath = input.FilePath.TrimStart('/');
        var destinationPath = $"{containerName}/{blobPath}";

        // Create the operation record
        var operation = new ArchiveOperation
        {
            ClientTenantId = input.TenantId,
            MspOrgId = input.MspOrgId,
            FileMetadataId = input.FileMetadataId,
            OperationId = operationId,
            Action = "Archive",
            SourcePath = input.FilePath,
            DestinationPath = destinationPath,
            TargetTier = input.TargetTier,
            Status = "InProgress",
        };

        _db.ArchiveOperations.Add(operation);
        await _db.SaveChangesAsync(cancellationToken);

        try
        {
            // Step 1: Download file from SharePoint via Graph API
            _logger.LogInformation(
                "Downloading file {FileName} from drive {DriveId} for tenant {TenantId}",
                input.FileName, input.DriveId, input.TenantId);

            var graphClient = await CreateGraphClientAsync(input.M365TenantId, cancellationToken);
            using var fileStream = await graphClient.Drives[input.DriveId]
                .Items[input.ItemId]
                .Content
                .GetAsync(cancellationToken: cancellationToken);

            if (fileStream is null)
                throw new InvalidOperationException($"Failed to download file content for {input.FileName}");

            // Step 2: Upload to Azure Blob Storage
            var containerClient = _blobClient.GetBlobContainerClient(containerName);
            await containerClient.CreateIfNotExistsAsync(cancellationToken: cancellationToken);

            var blobClient = containerClient.GetBlobClient(blobPath);

            var metadata = new Dictionary<string, string>
            {
                ["originalSiteId"] = input.SiteId,
                ["originalDriveId"] = input.DriveId,
                ["originalItemId"] = input.ItemId,
                ["originalFileName"] = input.FileName,
                ["originalFilePath"] = input.FilePath,
                ["originalOwner"] = input.Owner ?? string.Empty,
                ["lastModifiedAt"] = input.LastModifiedAt.ToString("O"),
                ["archivedAt"] = DateTimeOffset.UtcNow.ToString("O"),
                ["targetTier"] = input.TargetTier,
            };

            var uploadOptions = new BlobUploadOptions
            {
                Metadata = metadata,
                AccessTier = MapToAccessTier(input.TargetTier),
            };

            await blobClient.UploadAsync(fileStream, uploadOptions, cancellationToken);

            _logger.LogInformation(
                "Uploaded file {FileName} to blob {BlobPath} in container {Container}",
                input.FileName, blobPath, containerName);

            // Step 3: Verify blob exists and size matches (NFR28)
            var properties = await blobClient.GetPropertiesAsync(cancellationToken: cancellationToken);
            var blobSize = properties.Value.ContentLength;

            if (blobSize != input.SizeBytes)
            {
                _logger.LogError(
                    "Size mismatch for {FileName}: expected {Expected}, got {Actual}",
                    input.FileName, input.SizeBytes, blobSize);

                // Delete the mismatched blob
                await blobClient.DeleteIfExistsAsync(cancellationToken: cancellationToken);

                throw new InvalidOperationException(
                    $"Blob size mismatch for {input.FileName}: expected {input.SizeBytes}, got {blobSize}");
            }

            // Step 4: Update operation and file metadata status
            operation.Status = "Completed";
            operation.CompletedAt = DateTimeOffset.UtcNow;

            var fileMetadata = await _db.FileMetadata
                .FirstOrDefaultAsync(f => f.Id == input.FileMetadataId, cancellationToken);

            if (fileMetadata is not null)
            {
                fileMetadata.ArchiveStatus = "Archived";
                fileMetadata.BlobTier = input.TargetTier;
            }

            await _db.SaveChangesAsync(cancellationToken);

            _logger.LogInformation(
                "Archive operation {OperationId} completed for file {FileName}",
                operationId, input.FileName);

            await _auditService.LogAsync(new AuditInput
            {
                MspOrgId = input.MspOrgId,
                ClientTenantId = input.TenantId,
                Action = "Archive",
                CorrelationId = operationId,
                Details = new
                {
                    sourcePath = input.FilePath,
                    destinationBlob = $"tenant-{input.TenantId}/{input.FilePath.TrimStart('/')}",
                    fileSize = input.SizeBytes,
                    targetTier = input.TargetTier,
                    approvedBy = operation.ApprovedBy ?? "auto-approved",
                    operationId,
                },
            }, cancellationToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogError(ex,
                "Archive operation {OperationId} failed for file {FileName}: {Error}",
                operationId, input.FileName, ex.Message);

            operation.Status = "Failed";
            operation.ErrorMessage = ex.Message.Length > 2000
                ? ex.Message[..2000]
                : ex.Message;
            operation.CompletedAt = DateTimeOffset.UtcNow;

            // Update file metadata to Failed status
            var fileMetadata = await _db.FileMetadata
                .FirstOrDefaultAsync(f => f.Id == input.FileMetadataId, cancellationToken);

            if (fileMetadata is not null)
                fileMetadata.ArchiveStatus = "Failed";

            await _db.SaveChangesAsync(cancellationToken);
        }

        return MapToDto(operation);
    }

    public async Task<List<ArchiveFileInput>> GetFilesToArchiveAsync(
        Guid tenantId,
        Guid mspOrgId,
        Guid? ruleId = null,
        CancellationToken cancellationToken = default)
    {
        // Get the evaluation results (files that match archive rules, excluding excluded files)
        var evaluationResults = await _evaluationService.EvaluateAllFilesAsync(
            tenantId, mspOrgId, cancellationToken);

        var matchedFiles = evaluationResults
            .Where(r => r.MatchedArchiveRuleId.HasValue && !r.IsExcluded);

        if (ruleId.HasValue)
            matchedFiles = matchedFiles.Where(r => r.MatchedArchiveRuleId == ruleId.Value);

        var fileIds = matchedFiles.Select(r => r.FileId).ToList();

        if (fileIds.Count == 0)
            return [];

        // Load file metadata for matched files
        var tenant = await _db.ClientTenants
            .AsNoTracking()
            .FirstOrDefaultAsync(t => t.Id == tenantId && t.MspOrgId == mspOrgId, cancellationToken);

        if (tenant is null)
            throw new InvalidOperationException("Tenant not found.");

        var files = await _db.FileMetadata
            .AsNoTracking()
            .Where(f => fileIds.Contains(f.Id) && f.ArchiveStatus == "Active")
            .ToListAsync(cancellationToken);

        // Map evaluation results with tier info
        var resultLookup = evaluationResults
            .Where(r => r.MatchedArchiveRuleId.HasValue)
            .ToDictionary(r => r.FileId);

        return files.Select(f =>
        {
            resultLookup.TryGetValue(f.Id, out var evalResult);
            return new ArchiveFileInput
            {
                TenantId = tenantId,
                MspOrgId = mspOrgId,
                M365TenantId = tenant.M365TenantId,
                FileMetadataId = f.Id,
                SiteId = f.SiteId,
                DriveId = f.DriveId,
                ItemId = f.ItemId,
                FileName = f.FileName,
                FilePath = f.FilePath,
                SizeBytes = f.SizeBytes,
                Owner = f.Owner,
                LastModifiedAt = f.LastModifiedAt,
                TargetTier = evalResult?.TargetTier ?? "Cool",
                RuleId = evalResult?.MatchedArchiveRuleId,
            };
        }).ToList();
    }

    public async Task<List<ArchiveOperationDto>> GetOperationsAsync(
        Guid tenantId,
        Guid mspOrgId,
        string? status = null,
        CancellationToken cancellationToken = default)
    {
        var query = _db.ArchiveOperations
            .AsNoTracking()
            .Where(o => o.ClientTenantId == tenantId && o.MspOrgId == mspOrgId);

        if (!string.IsNullOrEmpty(status))
            query = query.Where(o => o.Status == status);

        return await query
            .OrderByDescending(o => o.CreatedAt)
            .Take(100)
            .Select(o => new ArchiveOperationDto
            {
                Id = o.Id,
                FileMetadataId = o.FileMetadataId,
                OperationId = o.OperationId,
                Action = o.Action,
                SourcePath = o.SourcePath,
                DestinationPath = o.DestinationPath,
                TargetTier = o.TargetTier,
                Status = o.Status,
                ErrorMessage = o.ErrorMessage,
                CreatedAt = o.CreatedAt,
                CompletedAt = o.CompletedAt,
            })
            .ToListAsync(cancellationToken);
    }

    public async Task<RetrievalOperationDto> RetrieveFileAsync(
        RetrieveFileInput input,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(input);

        const long maxRetrievalSizeBytes = 250 * 1024 * 1024; // 250 MB — streaming limit for single PUT upload
        if (input.SizeBytes > maxRetrievalSizeBytes)
            throw new InvalidOperationException(
                $"File {input.FileName} ({input.SizeBytes / (1024 * 1024)} MB) exceeds the 250 MB retrieval limit. Large file retrieval requires upload session support.");

        var retrieveOpId = $"retrieve-{input.FileMetadataId}-{DateTimeOffset.UtcNow.Ticks}";

        // Create a retrieval operation record
        var operation = new ArchiveOperation
        {
            ClientTenantId = input.TenantId,
            MspOrgId = input.MspOrgId,
            FileMetadataId = input.FileMetadataId,
            OperationId = retrieveOpId,
            Action = "Retrieve",
            SourcePath = $"tenant-{input.TenantId}/{input.FilePath.TrimStart('/')}",
            DestinationPath = input.FilePath,
            TargetTier = input.BlobTier ?? "Cool",
            Status = "InProgress",
        };

        _db.ArchiveOperations.Add(operation);
        await _db.SaveChangesAsync(cancellationToken);

        try
        {
            // Step 1: Download file from Blob Storage
            var containerName = $"tenant-{input.TenantId}";
            var blobPath = input.FilePath.TrimStart('/');

            var containerClient = _blobClient.GetBlobContainerClient(containerName);
            var blobClient = containerClient.GetBlobClient(blobPath);

            if (!await blobClient.ExistsAsync(cancellationToken))
                throw new InvalidOperationException($"Blob not found: {containerName}/{blobPath}");

            _logger.LogInformation(
                "Downloading blob {BlobPath} from container {Container} for retrieval",
                blobPath, containerName);

            using var blobStream = await blobClient.OpenReadAsync(cancellationToken: cancellationToken);

            // Step 2: Upload back to SharePoint via Graph API
            var graphClient = await CreateGraphClientAsync(input.M365TenantId, cancellationToken);

            _logger.LogInformation(
                "Uploading file {FileName} back to drive {DriveId} item {ItemId}",
                input.FileName, input.DriveId, input.ItemId);

            await graphClient.Drives[input.DriveId]
                .Items[input.ItemId]
                .Content
                .PutAsync(blobStream, cancellationToken: cancellationToken);

            // Step 3: Update operation and file metadata
            operation.Status = "Completed";
            operation.CompletedAt = DateTimeOffset.UtcNow;

            var fileMetadata = await _db.FileMetadata
                .FirstOrDefaultAsync(f => f.Id == input.FileMetadataId, cancellationToken);

            if (fileMetadata is not null)
            {
                fileMetadata.ArchiveStatus = "Retrieved";
            }

            await _db.SaveChangesAsync(cancellationToken);

            _logger.LogInformation(
                "Retrieval operation {OperationId} completed for file {FileName}",
                retrieveOpId, input.FileName);

            await _auditService.LogAsync(new AuditInput
            {
                MspOrgId = input.MspOrgId,
                ClientTenantId = input.TenantId,
                Action = "Retrieve",
                CorrelationId = retrieveOpId,
                Details = new
                {
                    sourceBlob = $"tenant-{input.TenantId}/{input.FilePath.TrimStart('/')}",
                    destinationPath = input.FilePath,
                    fileSize = input.SizeBytes,
                    blobTier = input.BlobTier ?? "Cool",
                    operationId = retrieveOpId,
                },
            }, cancellationToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogError(ex,
                "Retrieval operation {OperationId} failed for file {FileName}: {Error}",
                retrieveOpId, input.FileName, ex.Message);

            operation.Status = "Failed";
            operation.ErrorMessage = ex.Message.Length > 2000 ? ex.Message[..2000] : ex.Message;
            operation.CompletedAt = DateTimeOffset.UtcNow;

            await _db.SaveChangesAsync(cancellationToken);
        }

        return new RetrievalOperationDto
        {
            Id = operation.Id,
            FileMetadataId = operation.FileMetadataId,
            FileName = input.FileName,
            FilePath = input.FilePath,
            SizeBytes = input.SizeBytes,
            BlobTier = input.BlobTier ?? "Cool",
            Status = operation.Status,
            ErrorMessage = operation.ErrorMessage,
            CreatedAt = operation.CreatedAt,
            CompletedAt = operation.CompletedAt,
        };
    }

    private static string GenerateOperationId(Guid fileMetadataId, Guid? ruleId)
    {
        var input = $"{fileMetadataId}-{ruleId ?? Guid.Empty}";
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(input));
        return Convert.ToHexString(hash)[..32].ToLowerInvariant();
    }

    private static AccessTier MapToAccessTier(string targetTier)
    {
        return targetTier switch
        {
            "Cool" => AccessTier.Cool,
            "Cold" => AccessTier.Cold,
            "Archive" => AccessTier.Archive,
            _ => AccessTier.Cool,
        };
    }

    private async Task<GraphServiceClient> CreateGraphClientAsync(string m365TenantId, CancellationToken cancellationToken)
    {
        var clientSecret = await _keyVaultService.GetSecretAsync(ClientSecretName, cancellationToken);

        var credential = new ClientSecretCredential(
            m365TenantId,
            _entraIdOptions.ClientId,
            clientSecret,
            new ClientSecretCredentialOptions { AuthorityHost = AzureAuthorityHosts.AzurePublicCloud });

        var scopes = new[] { "https://graph.microsoft.com/.default" };
        return new GraphServiceClient(credential, scopes);
    }

    private static ArchiveOperationDto MapToDto(ArchiveOperation entity)
    {
        return new ArchiveOperationDto
        {
            Id = entity.Id,
            FileMetadataId = entity.FileMetadataId,
            OperationId = entity.OperationId,
            Action = entity.Action,
            SourcePath = entity.SourcePath,
            DestinationPath = entity.DestinationPath,
            TargetTier = entity.TargetTier,
            Status = entity.Status,
            ErrorMessage = entity.ErrorMessage,
            CreatedAt = entity.CreatedAt,
            CompletedAt = entity.CompletedAt,
        };
    }
}
