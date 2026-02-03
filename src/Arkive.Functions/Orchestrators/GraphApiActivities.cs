using Arkive.Core.Interfaces;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;

namespace Arkive.Functions.Orchestrators;

public class GraphApiActivities
{
    private readonly IScanService _scanService;
    private readonly ILogger<GraphApiActivities> _logger;

    public GraphApiActivities(IScanService scanService, ILogger<GraphApiActivities> logger)
    {
        _scanService = scanService;
        _logger = logger;
    }

    [Function(nameof(GetSelectedSites))]
    public async Task<List<SelectedSiteInfo>> GetSelectedSites(
        [ActivityTrigger] Guid tenantId,
        CancellationToken cancellationToken)
    {
        _logger.LogInformation("Getting selected sites for tenant {TenantId}", tenantId);

        var sites = await _scanService.GetSelectedSitesAsync(tenantId, cancellationToken);

        return sites.Select(s => new SelectedSiteInfo { SiteId = s.SiteId }).ToList();
    }

    [Function(nameof(EnumerateFilesForSite))]
    public async Task<SiteFilesResult> EnumerateFilesForSite(
        [ActivityTrigger] SiteEnumerationInput input,
        CancellationToken cancellationToken)
    {
        _logger.LogInformation("Enumerating files for site {SiteId} in tenant {M365TenantId}",
            input.SiteId, input.M365TenantId);

        var files = await _scanService.EnumerateFilesForSiteAsync(
            input.M365TenantId, input.SiteId, cancellationToken);

        var batchItems = files.Select(f => new FileMetadataBatchItem
        {
            SiteId = f.SiteId,
            DriveId = f.DriveId,
            ItemId = f.ItemId,
            FileName = f.FileName,
            FilePath = f.FilePath,
            FileType = f.FileType,
            SizeBytes = f.SizeBytes,
            Owner = f.Owner,
            CreatedAt = f.CreatedAt,
            LastModifiedAt = f.LastModifiedAt
        }).ToList();

        _logger.LogInformation("Enumerated {FileCount} files for site {SiteId}",
            batchItems.Count, input.SiteId);

        return new SiteFilesResult
        {
            SiteId = input.SiteId,
            Files = batchItems
        };
    }

    [Function(nameof(SaveFileMetadataBatch))]
    public async Task<int> SaveFileMetadataBatch(
        [ActivityTrigger] SaveFileMetadataInput input,
        CancellationToken cancellationToken)
    {
        _logger.LogInformation("Saving {FileCount} file metadata records for tenant {TenantId}",
            input.Files.Count, input.ClientTenantId);

        var dtos = input.Files.Select(f => new FileMetadataDto
        {
            SiteId = f.SiteId,
            DriveId = f.DriveId,
            ItemId = f.ItemId,
            FileName = f.FileName,
            FilePath = f.FilePath,
            FileType = f.FileType,
            SizeBytes = f.SizeBytes,
            Owner = f.Owner,
            CreatedAt = f.CreatedAt,
            LastModifiedAt = f.LastModifiedAt
        }).ToList();

        var count = await _scanService.UpsertFileMetadataBatchAsync(
            input.ClientTenantId, input.MspOrgId, dtos, cancellationToken);

        _logger.LogInformation("Upserted {Count} file metadata records for tenant {TenantId}",
            count, input.ClientTenantId);

        return count;
    }

    [Function(nameof(UpdateScanTimestamp))]
    public async Task UpdateScanTimestamp(
        [ActivityTrigger] Guid tenantId,
        CancellationToken cancellationToken)
    {
        _logger.LogInformation("Updating scan timestamp for tenant {TenantId}", tenantId);
        await _scanService.UpdateScanTimestampAsync(tenantId, cancellationToken);
    }
}
