using Arkive.Core.Models;

namespace Arkive.Core.Interfaces;

public interface IScanService
{
    Task<List<SharePointSite>> GetSelectedSitesAsync(Guid tenantId, CancellationToken cancellationToken = default);
    Task<List<FileMetadataDto>> EnumerateFilesForSiteAsync(string m365TenantId, string siteId, CancellationToken cancellationToken = default);
    Task<int> UpsertFileMetadataBatchAsync(Guid clientTenantId, Guid mspOrgId, List<FileMetadataDto> files, CancellationToken cancellationToken = default);
    Task UpdateScanTimestampAsync(Guid tenantId, CancellationToken cancellationToken = default);
}

public class FileMetadataDto
{
    public string SiteId { get; set; } = string.Empty;
    public string DriveId { get; set; } = string.Empty;
    public string ItemId { get; set; } = string.Empty;
    public string FileName { get; set; } = string.Empty;
    public string FilePath { get; set; } = string.Empty;
    public string FileType { get; set; } = string.Empty;
    public long SizeBytes { get; set; }
    public string? Owner { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset LastModifiedAt { get; set; }
}
