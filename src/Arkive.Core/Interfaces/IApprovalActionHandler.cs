using Arkive.Core.DTOs;

namespace Arkive.Core.Interfaces;

public interface IApprovalActionHandler
{
    /// <summary>
    /// Processes an approval action (approve/reject/review) from a Teams Adaptive Card.
    /// Records the action in the database and signals the orchestrator.
    /// </summary>
    Task<ApprovalActionResult> HandleActionAsync(
        ApprovalActionInput input,
        CancellationToken cancellationToken = default);
}
