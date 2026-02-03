namespace Arkive.Core.DTOs;

public class SiteFilesDto
{
    public string SiteId { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public FileSummaryDto Summary { get; set; } = new();
    public List<FileDetailDto> Files { get; set; } = [];
    public int Page { get; set; }
    public int PageSize { get; set; }
    public int TotalCount { get; set; }
    public int TotalPages { get; set; }
}

public class FileSummaryDto
{
    public int TotalFileCount { get; set; }
    public long TotalSizeBytes { get; set; }
    public int StaleFileCount { get; set; }
    public long StaleSizeBytes { get; set; }
    public int StaleDaysThreshold { get; set; }
}

public class FileDetailDto
{
    public Guid Id { get; set; }
    public string FileName { get; set; } = string.Empty;
    public string FilePath { get; set; } = string.Empty;
    public string FileType { get; set; } = string.Empty;
    public long SizeBytes { get; set; }
    public string? Owner { get; set; }
    public DateTimeOffset LastModifiedAt { get; set; }
    public DateTimeOffset? LastAccessedAt { get; set; }
    public string ArchiveStatus { get; set; } = "Active";
    public bool IsStale { get; set; }
}
