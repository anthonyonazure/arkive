using Arkive.Core.DTOs;

namespace Arkive.Core.Interfaces;

public interface IArchiveService
{
    /// <summary>
    /// Archives a single file: downloads from SharePoint, uploads to Blob Storage,
    /// verifies the upload, and creates an ArchiveOperation record.
    /// </summary>
    Task<ArchiveOperationDto> ArchiveFileAsync(
        ArchiveFileInput input,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the list of files eligible for archiving based on active rules,
    /// excluding files matched by exclusion rules and already-archived files.
    /// </summary>
    Task<List<ArchiveFileInput>> GetFilesToArchiveAsync(
        Guid tenantId,
        Guid mspOrgId,
        Guid? ruleId = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the archive operation status for a tenant.
    /// </summary>
    Task<List<ArchiveOperationDto>> GetOperationsAsync(
        Guid tenantId,
        Guid mspOrgId,
        string? status = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves a single archived file from Blob Storage and uploads it back to SharePoint.
    /// </summary>
    Task<RetrievalOperationDto> RetrieveFileAsync(
        RetrieveFileInput input,
        CancellationToken cancellationToken = default);
}

public class RetrieveFileInput
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
    public string? BlobTier { get; set; }
}

public class ArchiveFileInput
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
    public string? Owner { get; set; }
    public DateTimeOffset LastModifiedAt { get; set; }
    public string TargetTier { get; set; } = "Cool";
    public Guid? RuleId { get; set; }
}
