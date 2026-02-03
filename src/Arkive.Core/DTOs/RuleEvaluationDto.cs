namespace Arkive.Core.DTOs;

public class RuleEvaluationResultDto
{
    public Guid FileId { get; set; }
    public string FileName { get; set; } = string.Empty;
    public bool IsExcluded { get; set; }
    public Guid? MatchedArchiveRuleId { get; set; }
    public Guid? MatchedExclusionRuleId { get; set; }
    public string? TargetTier { get; set; }
}

public class ExclusionScopeDto
{
    public Guid RuleId { get; set; }
    public int AffectedFileCount { get; set; }
    public long AffectedSizeBytes { get; set; }
}
