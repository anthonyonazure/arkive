namespace Arkive.Core.DTOs;

/// <summary>
/// A single archived file result in the search response.
/// </summary>
public class ArchivedFileDto
{
    public Guid FileMetadataId { get; set; }
    public string FileName { get; set; } = string.Empty;
    public string FilePath { get; set; } = string.Empty;
    public string FileType { get; set; } = string.Empty;
    public long SizeBytes { get; set; }
    public string? Owner { get; set; }
    public string SiteId { get; set; } = string.Empty;
    public string SiteName { get; set; } = string.Empty;
    public Guid TenantId { get; set; }
    public string TenantName { get; set; } = string.Empty;
    public string BlobTier { get; set; } = string.Empty;
    public string EstimatedRetrievalTime { get; set; } = string.Empty;
    public DateTimeOffset ArchivedAt { get; set; }
    public DateTimeOffset LastModifiedAt { get; set; }
}

/// <summary>
/// Paginated search results for archived files.
/// </summary>
public class ArchiveSearchResultDto
{
    public List<ArchivedFileDto> Files { get; set; } = [];
    public int Page { get; set; }
    public int PageSize { get; set; }
    public int TotalCount { get; set; }
    public int TotalPages { get; set; }
}
