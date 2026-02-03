namespace Arkive.Core.Models;

public class FileMetadata
{
    public Guid Id { get; set; }
    public Guid ClientTenantId { get; set; }
    public Guid MspOrgId { get; set; }
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
    public DateTimeOffset? LastAccessedAt { get; set; }
    public string ArchiveStatus { get; set; } = "Active";
    public string? BlobTier { get; set; }
    public DateTimeOffset ScannedAt { get; set; }

    // Navigation properties
    public ClientTenant ClientTenant { get; set; } = null!;
}
