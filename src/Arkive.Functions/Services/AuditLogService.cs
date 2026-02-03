using Arkive.Core.Configuration;
using Arkive.Core.Interfaces;
using Arkive.Data;
using Azure.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.Graph.Beta;
using Microsoft.Graph.Beta.Models.Security;
using Microsoft.Graph.Beta.Models.ODataErrors;

namespace Arkive.Functions.Services;

public class AuditLogService : IAuditLogService
{
    private readonly ArkiveDbContext _db;
    private readonly EntraIdOptions _entraIdOptions;
    private readonly IKeyVaultService _keyVaultService;
    private readonly ILogger<AuditLogService> _logger;

    private const string ClientSecretName = "arkive-client-secret";
    private const int MaxPollAttempts = 30;
    private static readonly TimeSpan PollInterval = TimeSpan.FromSeconds(10);

    // Operations that indicate a file was accessed/used
    private static readonly string[] FileAccessOperations =
    [
        "FileAccessed",
        "FileDownloaded",
        "FileViewed",
        "FileModified",
        "FileModifiedExtended"
    ];

    public AuditLogService(
        ArkiveDbContext db,
        IOptions<EntraIdOptions> entraIdOptions,
        IKeyVaultService keyVaultService,
        ILogger<AuditLogService> logger)
    {
        _db = db;
        _entraIdOptions = entraIdOptions.Value;
        _keyVaultService = keyVaultService;
        _logger = logger;
    }

    public async Task<AuditLogResult> GetLastAccessedDatesAsync(
        string m365TenantId,
        Guid clientTenantId,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(m365TenantId);

        _logger.LogInformation("Starting audit log query for tenant {TenantId} (M365: {M365TenantId})",
            clientTenantId, m365TenantId);

        GraphServiceClient betaClient;
        try
        {
            betaClient = await CreateBetaGraphClientAsync(m365TenantId, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to create Graph client for audit log query, tenant {TenantId}", clientTenantId);
            return await ApplyFallbackAsync(clientTenantId, cancellationToken);
        }

        try
        {
            // Determine query window: from last scan or default 90 days
            var tenant = await _db.ClientTenants
                .AsNoTracking()
                .FirstOrDefaultAsync(t => t.Id == clientTenantId, cancellationToken);

            var lookbackStart = tenant?.LastScannedAt?.AddDays(-1)
                ?? DateTimeOffset.UtcNow.AddDays(-90);

            var lookbackEnd = DateTimeOffset.UtcNow;

            // Step 1: Create audit log query
            var queryId = await CreateAuditLogQueryAsync(betaClient, lookbackStart, lookbackEnd, cancellationToken);
            if (queryId is null)
            {
                _logger.LogWarning("Audit log query creation returned null for tenant {TenantId}, applying fallback", clientTenantId);
                return await ApplyFallbackAsync(clientTenantId, cancellationToken);
            }

            // Step 2: Poll for query completion
            var succeeded = await PollQueryCompletionAsync(betaClient, queryId, cancellationToken);
            if (!succeeded)
            {
                _logger.LogWarning("Audit log query did not complete for tenant {TenantId}, applying fallback", clientTenantId);
                return await ApplyFallbackAsync(clientTenantId, cancellationToken);
            }

            // Step 3: Retrieve records and build access map
            var accessMap = await RetrieveAccessMapAsync(betaClient, queryId, cancellationToken);

            _logger.LogInformation("Retrieved {EventCount} file access events from audit log for tenant {TenantId}",
                accessMap.Count, clientTenantId);

            // Step 4: Update FileMetadata records with LastAccessedAt
            var updatedCount = await UpdateLastAccessedDatesAsync(clientTenantId, accessMap, cancellationToken);

            // Step 5: Apply fallback for files that still have no LastAccessedAt
            var fallbackCount = await SetFallbackLastAccessedAsync(clientTenantId, cancellationToken);

            // Mark audit log as available for this tenant
            await UpdateAuditLogAvailabilityAsync(clientTenantId, true, cancellationToken);

            _logger.LogInformation(
                "Audit log processing complete for tenant {TenantId}: {UpdatedCount} from audit log, {FallbackCount} from fallback",
                clientTenantId, updatedCount, fallbackCount);

            return new AuditLogResult
            {
                FilesUpdated = updatedCount + fallbackCount,
                AuditLogAvailable = true,
                FallbackApplied = fallbackCount > 0
            };
        }
        catch (ODataError ex) when (ex.ResponseStatusCode is 403 or 401)
        {
            _logger.LogWarning(
                "Audit log access denied for tenant {TenantId} (HTTP {StatusCode}). Tenant likely lacks E3+ licensing. Applying fallback.",
                clientTenantId, ex.ResponseStatusCode);

            await UpdateAuditLogAvailabilityAsync(clientTenantId, false, cancellationToken);
            return await ApplyFallbackAsync(clientTenantId, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error querying audit log for tenant {TenantId}. Applying fallback.", clientTenantId);
            await UpdateAuditLogAvailabilityAsync(clientTenantId, false, cancellationToken);
            return await ApplyFallbackAsync(clientTenantId, cancellationToken);
        }
    }

    public async Task UpdateAuditLogAvailabilityAsync(Guid clientTenantId, bool available, CancellationToken cancellationToken = default)
    {
        var tenant = await _db.ClientTenants.FindAsync([clientTenantId], cancellationToken);
        if (tenant is null)
        {
            _logger.LogWarning("Tenant {TenantId} not found when updating audit log availability", clientTenantId);
            return;
        }

        tenant.AuditLogAvailable = available;
        await _db.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Updated AuditLogAvailable = {Available} for tenant {TenantId}", available, clientTenantId);
    }

    private async Task<string?> CreateAuditLogQueryAsync(
        GraphServiceClient betaClient,
        DateTimeOffset startTime,
        DateTimeOffset endTime,
        CancellationToken cancellationToken)
    {
        var query = new AuditLogQuery
        {
            DisplayName = $"Arkive FileAccess Scan {DateTimeOffset.UtcNow:yyyy-MM-dd HH:mm}",
            FilterStartDateTime = startTime,
            FilterEndDateTime = endTime,
            OperationFilters = [.. FileAccessOperations],
            RecordTypeFilters = [AuditLogRecordType.SharePointFileOperation]
        };

        var result = await betaClient.Security.AuditLog.Queries.PostAsync(query, cancellationToken: cancellationToken);
        var queryId = result?.Id;

        _logger.LogInformation("Created audit log query {QueryId} for window {Start} to {End}",
            queryId, startTime, endTime);

        return queryId;
    }

    private async Task<bool> PollQueryCompletionAsync(
        GraphServiceClient betaClient,
        string queryId,
        CancellationToken cancellationToken)
    {
        for (var attempt = 0; attempt < MaxPollAttempts; attempt++)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var queryStatus = await betaClient.Security.AuditLog.Queries[queryId]
                .GetAsync(cancellationToken: cancellationToken);

            var status = queryStatus?.Status;

            if (status == AuditLogQueryStatus.Succeeded)
            {
                _logger.LogInformation("Audit log query {QueryId} succeeded after {Attempts} poll attempts", queryId, attempt + 1);
                return true;
            }

            if (status == AuditLogQueryStatus.Failed)
            {
                _logger.LogWarning("Audit log query {QueryId} failed", queryId);
                return false;
            }

            if (status == AuditLogQueryStatus.Cancelled)
            {
                _logger.LogWarning("Audit log query {QueryId} was cancelled", queryId);
                return false;
            }

            _logger.LogDebug("Audit log query {QueryId} status: {Status}, polling again (attempt {Attempt}/{Max})",
                queryId, status, attempt + 1, MaxPollAttempts);

            await Task.Delay(PollInterval, cancellationToken);
        }

        _logger.LogWarning("Audit log query {QueryId} timed out after {MaxAttempts} poll attempts", queryId, MaxPollAttempts);
        return false;
    }

    private async Task<Dictionary<string, DateTimeOffset>> RetrieveAccessMapAsync(
        GraphServiceClient betaClient,
        string queryId,
        CancellationToken cancellationToken)
    {
        // Map: objectId (file URL/path) → most recent access timestamp
        var accessMap = new Dictionary<string, DateTimeOffset>(StringComparer.OrdinalIgnoreCase);

        var page = await betaClient.Security.AuditLog.Queries[queryId].Records
            .GetAsync(config =>
            {
                config.QueryParameters.Top = 500;
            }, cancellationToken: cancellationToken);

        while (page?.Value is not null)
        {
            foreach (var record in page.Value)
            {
                if (record.ObjectId is null || record.CreatedDateTime is null)
                    continue;

                var objectId = record.ObjectId;
                var eventTime = record.CreatedDateTime.Value;

                // Keep the most recent access time per objectId
                if (accessMap.TryGetValue(objectId, out var existing))
                {
                    if (eventTime > existing)
                        accessMap[objectId] = eventTime;
                }
                else
                {
                    accessMap[objectId] = eventTime;
                }
            }

            // Handle pagination
            if (page.OdataNextLink is not null)
            {
                page = await betaClient.Security.AuditLog.Queries[queryId].Records
                    .WithUrl(page.OdataNextLink)
                    .GetAsync(cancellationToken: cancellationToken);
            }
            else
            {
                break;
            }
        }

        return accessMap;
    }

    private async Task<int> UpdateLastAccessedDatesAsync(
        Guid clientTenantId,
        Dictionary<string, DateTimeOffset> accessMap,
        CancellationToken cancellationToken)
    {
        if (accessMap.Count == 0) return 0;

        // Build a lookup: normalized file path suffix → most recent access time
        // objectId from audit log is typically a full SharePoint URL:
        // https://tenant.sharepoint.com/sites/SiteName/Shared Documents/folder/file.docx
        // filePath is stored as: Shared Documents/folder/file.docx
        // We extract the path suffix from objectId to match against stored FilePath
        var pathToAccessTime = new Dictionary<string, DateTimeOffset>(StringComparer.OrdinalIgnoreCase);

        foreach (var (objectId, accessTime) in accessMap)
        {
            var filePath = ExtractFilePathFromObjectId(objectId);
            if (filePath is null) continue;

            if (pathToAccessTime.TryGetValue(filePath, out var existing))
            {
                if (accessTime > existing)
                    pathToAccessTime[filePath] = accessTime;
            }
            else
            {
                pathToAccessTime[filePath] = accessTime;
            }
        }

        // Update in batches using ExecuteUpdateAsync per file path to avoid loading all records
        var updatedCount = 0;
        const int batchSize = 100;
        var pathEntries = pathToAccessTime.ToList();

        for (var i = 0; i < pathEntries.Count; i += batchSize)
        {
            var batch = pathEntries.Skip(i).Take(batchSize).ToList();

            foreach (var (filePath, accessTime) in batch)
            {
                var count = await _db.FileMetadata
                    .Where(f => f.ClientTenantId == clientTenantId
                        && f.FilePath == filePath
                        && (f.LastAccessedAt == null || f.LastAccessedAt < accessTime))
                    .ExecuteUpdateAsync(
                        setters => setters.SetProperty(f => f.LastAccessedAt, accessTime),
                        cancellationToken);

                updatedCount += count;
            }
        }

        if (updatedCount > 0)
        {
            _logger.LogInformation("Updated LastAccessedAt for {Count} files in tenant {TenantId}",
                updatedCount, clientTenantId);
        }

        return updatedCount;
    }

    /// <summary>
    /// Extracts the SharePoint-relative file path from an audit log objectId URL.
    /// Example: "https://tenant.sharepoint.com/sites/SiteName/Shared Documents/folder/file.docx"
    ///       → "Shared Documents/folder/file.docx"
    /// </summary>
    private static string? ExtractFilePathFromObjectId(string objectId)
    {
        if (string.IsNullOrEmpty(objectId))
            return null;

        var normalized = objectId.Replace('\\', '/');

        // Look for common SharePoint document library path segments
        // Pattern: https://tenant.sharepoint.com/sites/SiteName/LibraryName/path/file.ext
        // The path after the site name is what we store in FilePath
        var markers = new[] { "/Shared Documents/", "/Documents/", "/Shared%20Documents/" };
        foreach (var marker in markers)
        {
            var idx = normalized.IndexOf(marker, StringComparison.OrdinalIgnoreCase);
            if (idx >= 0)
            {
                // Return from the marker start (excluding leading slash)
                var path = normalized[(idx + 1)..];
                return Uri.UnescapeDataString(path);
            }
        }

        // Fallback: try to extract everything after /sites/SiteName/
        var sitesIdx = normalized.IndexOf("/sites/", StringComparison.OrdinalIgnoreCase);
        if (sitesIdx >= 0)
        {
            var afterSites = normalized[(sitesIdx + 7)..]; // skip "/sites/"
            var slashIdx = afterSites.IndexOf('/');
            if (slashIdx >= 0 && slashIdx < afterSites.Length - 1)
            {
                var path = afterSites[(slashIdx + 1)..];
                return Uri.UnescapeDataString(path);
            }
        }

        return null;
    }

    private async Task<int> SetFallbackLastAccessedAsync(
        Guid clientTenantId,
        CancellationToken cancellationToken)
    {
        // For files that have no LastAccessedAt, fall back to LastModifiedAt
        var count = await _db.FileMetadata
            .Where(f => f.ClientTenantId == clientTenantId && f.LastAccessedAt == null)
            .ExecuteUpdateAsync(
                setters => setters.SetProperty(f => f.LastAccessedAt, f => f.LastModifiedAt),
                cancellationToken);

        if (count > 0)
        {
            _logger.LogInformation(
                "Applied LastModifiedAt fallback for {Count} files in tenant {TenantId}",
                count, clientTenantId);
        }

        return count;
    }

    private async Task<AuditLogResult> ApplyFallbackAsync(
        Guid clientTenantId,
        CancellationToken cancellationToken)
    {
        var count = await SetFallbackLastAccessedAsync(clientTenantId, cancellationToken);

        return new AuditLogResult
        {
            FilesUpdated = count,
            AuditLogAvailable = false,
            FallbackApplied = true
        };
    }

    private async Task<GraphServiceClient> CreateBetaGraphClientAsync(
        string m365TenantId,
        CancellationToken cancellationToken)
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
}
