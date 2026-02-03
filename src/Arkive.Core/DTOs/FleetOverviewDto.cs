using Arkive.Core.Enums;

namespace Arkive.Core.DTOs;

public class FleetOverviewDto
{
    public FleetHeroSavingsDto HeroSavings { get; set; } = new();
    public List<FleetTenantDto> Tenants { get; set; } = [];
}

public class FleetHeroSavingsDto
{
    /// <summary>Monthly savings achieved so far (estimated from archived/archivable stale data).</summary>
    public decimal SavingsAchieved { get; set; }

    /// <summary>Total potential monthly savings if all stale data were archived.</summary>
    public decimal SavingsPotential { get; set; }

    /// <summary>Uncaptured savings remaining (potential - achieved).</summary>
    public decimal SavingsUncaptured { get; set; }

    /// <summary>Number of tenants included in the savings calculation.</summary>
    public int TenantCount { get; set; }
}

public class FleetTenantDto
{
    public Guid Id { get; set; }
    public string DisplayName { get; set; } = string.Empty;
    public TenantStatus Status { get; set; }
    public DateTimeOffset? ConnectedAt { get; set; }
    public int SelectedSiteCount { get; set; }
    public long TotalStorageBytes { get; set; }
    public long StaleStorageBytes { get; set; }
    public decimal SavingsAchieved { get; set; }
    public decimal SavingsPotential { get; set; }
    public double StalePercentage { get; set; }
    public DateTimeOffset? LastScanTime { get; set; }
    public string AttentionType { get; set; } = "all-clear";
    public int VetoCount { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
}
