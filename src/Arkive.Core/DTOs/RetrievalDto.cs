namespace Arkive.Core.DTOs;

/// <summary>
/// Request to retrieve archived files back to SharePoint.
/// </summary>
public class RetrievalRequest
{
    /// <summary>File metadata IDs to retrieve.</summary>
    public List<Guid> FileIds { get; set; } = [];
}

/// <summary>
/// Status of a single retrieval operation.
/// </summary>
public class RetrievalOperationDto
{
    public Guid Id { get; set; }
    public Guid FileMetadataId { get; set; }
    public string FileName { get; set; } = string.Empty;
    public string FilePath { get; set; } = string.Empty;
    public long SizeBytes { get; set; }
    public string BlobTier { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string? ErrorMessage { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset? CompletedAt { get; set; }
}

/// <summary>
/// Result of initiating a retrieval batch.
/// </summary>
public class RetrievalBatchResult
{
    public int TotalFiles { get; set; }
    public int Queued { get; set; }
    public int Skipped { get; set; }
    public string Message { get; set; } = string.Empty;
    public List<RetrievalOperationDto> Operations { get; set; } = [];
}
