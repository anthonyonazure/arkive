namespace Arkive.Core.Models;

public class MonthlySavingsSnapshot
{
    public Guid Id { get; set; }
    public Guid MspOrgId { get; set; }
    public Guid? ClientTenantId { get; set; }

    /// <summary>Year-month key, e.g. 2026-01.</summary>
    public string Month { get; set; } = string.Empty;

    public long TotalStorageBytes { get; set; }
    public long ArchivedStorageBytes { get; set; }
    public long StaleStorageBytes { get; set; }

    /// <summary>Net monthly savings achieved (SPO cost avoided minus Blob storage cost).</summary>
    public decimal SavingsAchieved { get; set; }

    /// <summary>Total potential monthly savings if all stale data were archived.</summary>
    public decimal SavingsPotential { get; set; }

    public DateTimeOffset CapturedAt { get; set; }

    // Navigation properties
    public MspOrganization MspOrganization { get; set; } = null!;
    public ClientTenant? ClientTenant { get; set; }
}
