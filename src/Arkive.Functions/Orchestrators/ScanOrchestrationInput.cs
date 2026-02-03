namespace Arkive.Functions.Orchestrators;

public class ScanOrchestrationInput
{
    public Guid TenantId { get; set; }
    public Guid MspOrgId { get; set; }
    public string M365TenantId { get; set; } = string.Empty;
}

public class SiteEnumerationInput
{
    public string M365TenantId { get; set; } = string.Empty;
    public string SiteId { get; set; } = string.Empty;
}

public class SaveFileMetadataInput
{
    public Guid ClientTenantId { get; set; }
    public Guid MspOrgId { get; set; }
    public List<FileMetadataBatchItem> Files { get; set; } = [];
}

public class FileMetadataBatchItem
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

public class AuditLogInput
{
    public string M365TenantId { get; set; } = string.Empty;
    public Guid ClientTenantId { get; set; }
}

public class AuditLogActivityResult
{
    public int FilesUpdated { get; set; }
    public bool AuditLogAvailable { get; set; }
    public bool FallbackApplied { get; set; }
}

public class ScanResultSummary
{
    public int SitesScanned { get; set; }
    public int TotalFiles { get; set; }
    public long TotalBytes { get; set; }
    public int AuditLogFilesUpdated { get; set; }
    public bool AuditLogAvailable { get; set; }
}

public class ScanJobMessage
{
    public Guid TenantId { get; set; }
    public Guid MspOrgId { get; set; }
    public string M365TenantId { get; set; } = string.Empty;
    public DateTimeOffset ScheduledAt { get; set; }
}

public class ScanCompletedEvent
{
    public Guid TenantId { get; set; }
    public ScanResultSummary Summary { get; set; } = new();
    public DateTimeOffset CompletedAt { get; set; }
    public bool Success { get; set; }
    public string? ErrorMessage { get; set; }
}
