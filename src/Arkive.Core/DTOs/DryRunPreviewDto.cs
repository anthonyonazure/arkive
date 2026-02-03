namespace Arkive.Core.DTOs;

public class DryRunPreviewDto
{
    public int FileCount { get; set; }
    public long TotalSizeBytes { get; set; }
    public decimal EstimatedAnnualSavings { get; set; }
    public List<SiteImpactDto> TopSites { get; set; } = [];
    public int ExcludedFileCount { get; set; }
}

public class SiteImpactDto
{
    public string SiteId { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public int FileCount { get; set; }
    public long SizeBytes { get; set; }
}

public class DryRunPreviewRequest
{
    public string RuleType { get; set; } = string.Empty;
    public string Criteria { get; set; } = "{}";
    public string TargetTier { get; set; } = "Cool";
}
