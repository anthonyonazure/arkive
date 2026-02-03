namespace Arkive.Core.DTOs;

public class SavingsTrendDto
{
    /// <summary>Year-month key, e.g. "2026-01".</summary>
    public string Month { get; set; } = string.Empty;

    public decimal SavingsAchieved { get; set; }
    public decimal SavingsPotential { get; set; }
    public long TotalStorageBytes { get; set; }
    public long ArchivedStorageBytes { get; set; }
}

public class SavingsTrendResult
{
    public List<SavingsTrendDto> Months { get; set; } = [];

    /// <summary>Current month values (real-time, not from snapshot).</summary>
    public SavingsTrendDto Current { get; set; } = new();

    /// <summary>Previous month snapshot for delta comparison.</summary>
    public SavingsTrendDto? Previous { get; set; }
}
