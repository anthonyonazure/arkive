namespace Arkive.Core.DTOs;

public class TenantAnalyticsDto
{
    public Guid TenantId { get; set; }
    public string DisplayName { get; set; } = string.Empty;

    // Stat cards
    public long TotalStorageBytes { get; set; }
    public decimal SavingsAchieved { get; set; }
    public decimal SavingsPotential { get; set; }

    // Cost analysis
    public CostAnalysisDto CostAnalysis { get; set; } = new();

    // Per-site breakdown
    public List<SiteBreakdownDto> Sites { get; set; } = [];
}

public class CostAnalysisDto
{
    /// <summary>Current estimated SharePoint spend per month.</summary>
    public decimal CurrentSpendPerMonth { get; set; }

    /// <summary>Potential savings if stale data archived to Cool tier.</summary>
    public decimal PotentialArchiveSavings { get; set; }

    /// <summary>Net monthly cost if fully optimized.</summary>
    public decimal NetCostIfOptimized { get; set; }
}

public class SiteBreakdownDto
{
    public string SiteId { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public long TotalStorageBytes { get; set; }
    public long ActiveStorageBytes { get; set; }
    public long StaleStorageBytes { get; set; }
    public double StalePercentage { get; set; }
    public decimal PotentialSavings { get; set; }
}
