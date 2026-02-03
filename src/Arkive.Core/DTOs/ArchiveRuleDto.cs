namespace Arkive.Core.DTOs;

public class ArchiveRuleDto
{
    public Guid Id { get; set; }
    public Guid ClientTenantId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string RuleType { get; set; } = string.Empty;
    public string Criteria { get; set; } = "{}";
    public string TargetTier { get; set; } = "Cool";
    public bool IsActive { get; set; }
    public string? CreatedBy { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
    public int? AffectedFileCount { get; set; }
    public long? AffectedSizeBytes { get; set; }
}

public class CreateArchiveRuleRequest
{
    public string Name { get; set; } = string.Empty;
    public string RuleType { get; set; } = string.Empty;
    public string Criteria { get; set; } = "{}";
    public string TargetTier { get; set; } = "Cool";
    public bool IsActive { get; set; } = true;
}

public class UpdateArchiveRuleRequest
{
    public string? Name { get; set; }
    public string? RuleType { get; set; }
    public string? Criteria { get; set; }
    public string? TargetTier { get; set; }
    public bool? IsActive { get; set; }
}
