namespace Arkive.Core.DTOs;

public class ArchiveOperationDto
{
    public Guid Id { get; set; }
    public Guid FileMetadataId { get; set; }
    public string OperationId { get; set; } = string.Empty;
    public string Action { get; set; } = string.Empty;
    public string SourcePath { get; set; } = string.Empty;
    public string DestinationPath { get; set; } = string.Empty;
    public string TargetTier { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string? ErrorMessage { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset? CompletedAt { get; set; }
}

public class TriggerArchiveRequest
{
    public Guid? RuleId { get; set; }
}

public class ArchiveStatusDto
{
    public string OrchestrationId { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public int TotalFiles { get; set; }
    public int CompletedFiles { get; set; }
    public int FailedFiles { get; set; }
    public DateTimeOffset? StartedAt { get; set; }
    public DateTimeOffset? CompletedAt { get; set; }
}
