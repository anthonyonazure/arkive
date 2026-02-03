namespace Arkive.Core.Models;

public class ArchiveOperation
{
    public Guid Id { get; set; }
    public Guid ClientTenantId { get; set; }
    public Guid MspOrgId { get; set; }
    public Guid FileMetadataId { get; set; }
    public string OperationId { get; set; } = string.Empty;
    public string Action { get; set; } = "Archive";
    public string SourcePath { get; set; } = string.Empty;
    public string DestinationPath { get; set; } = string.Empty;
    public string TargetTier { get; set; } = "Cool";
    public string Status { get; set; } = "Pending";
    public string? ApprovedBy { get; set; }
    public string? VetoedBy { get; set; }
    public string? VetoReason { get; set; }
    public DateTimeOffset? VetoedAt { get; set; }
    public string? ErrorMessage { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset? CompletedAt { get; set; }

    // Navigation properties
    public ClientTenant ClientTenant { get; set; } = null!;
    public FileMetadata FileMetadata { get; set; } = null!;
}
