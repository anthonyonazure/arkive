using Arkive.Core.DTOs;

namespace Arkive.Core.Interfaces;

public interface ISavingsSnapshotService
{
    /// <summary>Capture monthly snapshots for all orgs. Idempotent — upserts by org+tenant+month.</summary>
    Task CaptureSnapshotsAsync(CancellationToken cancellationToken = default);

    /// <summary>Get savings trend data for an org, optionally filtered by tenant.</summary>
    Task<SavingsTrendResult> GetTrendsAsync(Guid mspOrgId, Guid? tenantId = null, int months = 12, CancellationToken cancellationToken = default);
}
