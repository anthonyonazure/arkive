namespace Arkive.Core.DTOs;

public class SharePointSiteDto
{
    public string SiteId { get; set; } = string.Empty;
    public string Url { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public long StorageUsedBytes { get; set; }
    public bool IsSelected { get; set; }
}
