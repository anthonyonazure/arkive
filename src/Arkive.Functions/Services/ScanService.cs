using Arkive.Core.Configuration;
using Arkive.Core.Interfaces;
using Arkive.Core.Models;
using Arkive.Data;
using Azure.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.Graph;
using Microsoft.Graph.Models;
using Microsoft.Graph.Models.ODataErrors;

namespace Arkive.Functions.Services;

public class ScanService : IScanService
{
    private readonly ArkiveDbContext _db;
    private readonly EntraIdOptions _entraIdOptions;
    private readonly IKeyVaultService _keyVaultService;
    private readonly ILogger<ScanService> _logger;

    private const string ClientSecretName = "arkive-client-secret";
    private const int BatchSize = 500;

    public ScanService(
        ArkiveDbContext db,
        IOptions<EntraIdOptions> entraIdOptions,
        IKeyVaultService keyVaultService,
        ILogger<ScanService> logger)
    {
        _db = db;
        _entraIdOptions = entraIdOptions.Value;
        _keyVaultService = keyVaultService;
        _logger = logger;
    }

    public async Task<List<SharePointSite>> GetSelectedSitesAsync(Guid tenantId, CancellationToken cancellationToken = default)
    {
        return await _db.SharePointSites
            .Where(s => s.ClientTenantId == tenantId && s.IsSelected)
            .AsNoTracking()
            .ToListAsync(cancellationToken);
    }

    public async Task<List<FileMetadataDto>> EnumerateFilesForSiteAsync(string m365TenantId, string siteId, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(m365TenantId);
        ArgumentException.ThrowIfNullOrEmpty(siteId);

        var graphClient = await CreateGraphClientAsync(m365TenantId, cancellationToken);
        var allFiles = new List<FileMetadataDto>();

        _logger.LogInformation("Enumerating files for site {SiteId} in tenant {M365TenantId}", siteId, m365TenantId);

        try
        {
            // Get all drives (document libraries) for the site
            var drivesResponse = await graphClient.Sites[siteId].Drives.GetAsync(config =>
            {
                config.QueryParameters.Select = ["id", "name"];
            }, cancellationToken);

            var drives = drivesResponse?.Value ?? [];

            _logger.LogInformation("Found {DriveCount} drives for site {SiteId}", drives.Count, siteId);

            foreach (var drive in drives)
            {
                if (string.IsNullOrEmpty(drive.Id)) continue;

                var driveFiles = await EnumerateFilesInDriveAsync(graphClient, siteId, drive.Id, cancellationToken);
                allFiles.AddRange(driveFiles);
            }
        }
        catch (ODataError ex)
        {
            _logger.LogError(ex, "Graph API error enumerating files for site {SiteId}: {ErrorCode} {ErrorMessage}",
                siteId, ex.Error?.Code, ex.Error?.Message);
            throw;
        }

        _logger.LogInformation("Enumerated {FileCount} files for site {SiteId}", allFiles.Count, siteId);
        return allFiles;
    }

    public async Task<int> UpsertFileMetadataBatchAsync(Guid clientTenantId, Guid mspOrgId, List<FileMetadataDto> files, CancellationToken cancellationToken = default)
    {
        if (files.Count == 0) return 0;

        var now = DateTimeOffset.UtcNow;
        var upsertedCount = 0;

        // Process in batches to avoid memory and query size issues
        for (var i = 0; i < files.Count; i += BatchSize)
        {
            var batch = files.Skip(i).Take(BatchSize).ToList();

            // Get existing records by composite key for this batch
            // Build composite key set to avoid cross-join over-fetch
            var compositeKeys = batch.Select(f => new { f.SiteId, f.ItemId }).ToHashSet();
            var siteIds = compositeKeys.Select(k => k.SiteId).Distinct().ToList();

            var candidates = await _db.FileMetadata
                .Where(f => f.ClientTenantId == clientTenantId
                    && siteIds.Contains(f.SiteId))
                .ToListAsync(cancellationToken);

            var existing = candidates
                .Where(f => compositeKeys.Contains(new { f.SiteId, f.ItemId }))
                .ToDictionary(f => $"{f.SiteId}:{f.ItemId}");

            foreach (var file in batch)
            {
                var key = $"{file.SiteId}:{file.ItemId}";

                if (existing.TryGetValue(key, out var existingRecord))
                {
                    // Update existing record
                    existingRecord.FileName = file.FileName;
                    existingRecord.FilePath = file.FilePath;
                    existingRecord.FileType = file.FileType;
                    existingRecord.SizeBytes = file.SizeBytes;
                    existingRecord.Owner = file.Owner;
                    existingRecord.DriveId = file.DriveId;
                    existingRecord.LastModifiedAt = file.LastModifiedAt;
                    existingRecord.ScannedAt = now;
                }
                else
                {
                    // Insert new record
                    _db.FileMetadata.Add(new FileMetadata
                    {
                        ClientTenantId = clientTenantId,
                        MspOrgId = mspOrgId,
                        SiteId = file.SiteId,
                        DriveId = file.DriveId,
                        ItemId = file.ItemId,
                        FileName = file.FileName,
                        FilePath = file.FilePath,
                        FileType = file.FileType,
                        SizeBytes = file.SizeBytes,
                        Owner = file.Owner,
                        CreatedAt = file.CreatedAt,
                        LastModifiedAt = file.LastModifiedAt,
                        ScannedAt = now
                    });
                }
            }

            await _db.SaveChangesAsync(cancellationToken);
            upsertedCount += batch.Count;

            _logger.LogInformation("Upserted batch {BatchNumber}: {BatchCount} file records for tenant {TenantId}",
                (i / BatchSize) + 1, batch.Count, clientTenantId);
        }

        return upsertedCount;
    }

    public async Task UpdateScanTimestampAsync(Guid tenantId, CancellationToken cancellationToken = default)
    {
        var tenant = await _db.ClientTenants.FindAsync([tenantId], cancellationToken);
        if (tenant is null)
        {
            _logger.LogWarning("Tenant {TenantId} not found when updating scan timestamp", tenantId);
            return;
        }

        tenant.LastScannedAt = DateTimeOffset.UtcNow;
        await _db.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Updated LastScannedAt for tenant {TenantId}", tenantId);
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

    private async Task<List<FileMetadataDto>> EnumerateFilesInDriveAsync(
        GraphServiceClient graphClient,
        string siteId,
        string driveId,
        CancellationToken cancellationToken)
    {
        var files = new List<FileMetadataDto>();

        try
        {
            await EnumerateFolderAsync(graphClient, siteId, driveId, "root", files, cancellationToken);
        }
        catch (ODataError ex) when (ex.ResponseStatusCode == 404)
        {
            _logger.LogWarning("Drive {DriveId} not found or inaccessible in site {SiteId}", driveId, siteId);
        }

        return files;
    }

    private async Task EnumerateFolderAsync(
        GraphServiceClient graphClient,
        string siteId,
        string driveId,
        string folderId,
        List<FileMetadataDto> files,
        CancellationToken cancellationToken)
    {
        var folderIds = new List<string>();

        try
        {
            var page = await graphClient.Drives[driveId].Items[folderId].Children.GetAsync(config =>
            {
                config.QueryParameters.Select = [
                    "id", "name", "size", "file", "folder", "parentReference",
                    "createdDateTime", "lastModifiedDateTime", "lastModifiedBy"
                ];
            }, cancellationToken);

            // Process ALL pages, collecting both files and folder IDs
            while (page is not null)
            {
                if (page.Value is not null)
                {
                    foreach (var item in page.Value)
                    {
                        if (string.IsNullOrEmpty(item.Id)) continue;

                        if (item.Folder is not null)
                        {
                            folderIds.Add(item.Id);
                            continue;
                        }

                        if (item.File is null) continue;

                        var parentPath = item.ParentReference?.Path ?? string.Empty;
                        var colonIndex = parentPath.IndexOf(':');
                        var relativePath = colonIndex >= 0 ? parentPath[(colonIndex + 1)..] : parentPath;

                        files.Add(new FileMetadataDto
                        {
                            SiteId = siteId,
                            DriveId = driveId,
                            ItemId = item.Id,
                            FileName = item.Name ?? string.Empty,
                            FilePath = $"{relativePath}/{item.Name}".TrimStart('/'),
                            FileType = Path.GetExtension(item.Name ?? string.Empty),
                            SizeBytes = item.Size ?? 0,
                            Owner = item.LastModifiedBy?.User?.DisplayName,
                            CreatedAt = item.CreatedDateTime ?? DateTimeOffset.UtcNow,
                            LastModifiedAt = item.LastModifiedDateTime ?? DateTimeOffset.UtcNow
                        });
                    }
                }

                if (page.OdataNextLink is not null)
                {
                    page = await graphClient.Drives[driveId].Items[folderId].Children
                        .WithUrl(page.OdataNextLink)
                        .GetAsync(cancellationToken: cancellationToken);
                }
                else
                {
                    break;
                }
            }
        }
        catch (ODataError ex) when (ex.ResponseStatusCode == 404)
        {
            _logger.LogWarning("Folder {FolderId} not found in drive {DriveId}, site {SiteId}", folderId, driveId, siteId);
            return;
        }

        // Recurse into all discovered folders (from ALL pages)
        foreach (var childFolderId in folderIds)
        {
            await EnumerateFolderAsync(graphClient, siteId, driveId, childFolderId, files, cancellationToken);
        }
    }
}
