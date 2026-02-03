using Arkive.Core.DTOs;

namespace Arkive.Core.Interfaces;

public interface ITeamsNotificationService
{
    /// <summary>
    /// Sends an approval Adaptive Card to a site owner via Teams.
    /// Implements retry with 3x exponential backoff per NFR27.
    /// </summary>
    Task<ApprovalNotificationResult> SendApprovalCardAsync(
        ApprovalNotificationInput input,
        CancellationToken cancellationToken = default);
}
