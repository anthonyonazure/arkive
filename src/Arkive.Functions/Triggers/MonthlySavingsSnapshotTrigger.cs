using Arkive.Core.Interfaces;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;

namespace Arkive.Functions.Triggers;

public class MonthlySavingsSnapshotTrigger
{
    private readonly ISavingsSnapshotService _snapshotService;
    private readonly ILogger<MonthlySavingsSnapshotTrigger> _logger;

    public MonthlySavingsSnapshotTrigger(
        ISavingsSnapshotService snapshotService,
        ILogger<MonthlySavingsSnapshotTrigger> logger)
    {
        _snapshotService = snapshotService;
        _logger = logger;
    }

    /// <summary>
    /// Runs daily at 3 AM UTC. Captures/updates the current month's savings snapshot.
    /// Running daily ensures snapshots stay current even if a run is missed.
    /// </summary>
    [Function(nameof(MonthlySavingsSnapshotTrigger))]
    public async Task Run(
        [TimerTrigger("0 0 3 * * *")] TimerInfo timerInfo,
        CancellationToken cancellationToken)
    {
        _logger.LogInformation("Monthly savings snapshot trigger fired at {UtcNow}", DateTimeOffset.UtcNow);

        try
        {
            await _snapshotService.CaptureSnapshotsAsync(cancellationToken);
            _logger.LogInformation("Savings snapshot capture completed successfully");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to capture savings snapshots");
            throw;
        }
    }
}
