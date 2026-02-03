using Arkive.Core.DTOs;
using Arkive.Core.Interfaces;
using Arkive.Data;
using Microsoft.DurableTask.Client;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Arkive.Functions.Services;

public class ApprovalActionHandler : IApprovalActionHandler
{
    private readonly ArkiveDbContext _db;
    private readonly DurableTaskClient _durableClient;
    private readonly ILogger<ApprovalActionHandler> _logger;

    public ApprovalActionHandler(
        ArkiveDbContext db,
        DurableTaskClient durableClient,
        ILogger<ApprovalActionHandler> logger)
    {
        _db = db;
        _durableClient = durableClient;
        _logger = logger;
    }

    public async Task<ApprovalActionResult> HandleActionAsync(
        ApprovalActionInput input,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation(
            "Processing {Action} action from {Actor} for site {SiteId} in orchestration {OrchestrationId}",
            input.Action, input.ActorEmail, input.SiteId, input.OrchestrationInstanceId);

        if (!Guid.TryParse(input.TenantId, out var tenantId))
        {
            return new ApprovalActionResult
            {
                Success = false,
                Action = input.Action,
                Message = "Invalid tenant ID.",
            };
        }

        // Validate MspOrgId to prevent cross-organization access
        if (!Guid.TryParse(input.MspOrgId, out var mspOrgId))
        {
            return new ApprovalActionResult
            {
                Success = false,
                Action = input.Action,
                Message = "Invalid organization ID.",
            };
        }

        // Verify tenant belongs to the specified organization
        var tenantExists = await _db.ClientTenants
            .AnyAsync(t => t.Id == tenantId && t.MspOrgId == mspOrgId, cancellationToken);

        if (!tenantExists)
        {
            _logger.LogWarning(
                "Tenant {TenantId} not found in organization {MspOrgId} — rejecting {Action} action",
                tenantId, mspOrgId, input.Action);

            return new ApprovalActionResult
            {
                Success = false,
                Action = input.Action,
                Message = "Tenant not found.",
            };
        }

        return input.Action.ToLowerInvariant() switch
        {
            "approve" => await HandleApproveAsync(tenantId, input, cancellationToken),
            "reject" => await HandleRejectAsync(tenantId, input, cancellationToken),
            "review" => await HandleReviewAsync(tenantId, input, cancellationToken),
            _ => new ApprovalActionResult
            {
                Success = false,
                Action = input.Action,
                Message = $"Unknown action: {input.Action}",
            },
        };
    }

    private async Task<ApprovalActionResult> HandleApproveAsync(
        Guid tenantId, ApprovalActionInput input, CancellationToken cancellationToken)
    {
        // Find operations awaiting approval for this specific site
        var siteOperations = await _db.ArchiveOperations
            .Include(o => o.FileMetadata)
            .Where(o => o.ClientTenantId == tenantId
                && o.Status == "AwaitingApproval"
                && o.FileMetadata.SiteId == input.SiteId)
            .ToListAsync(cancellationToken);

        foreach (var op in siteOperations)
        {
            op.Status = "Approved";
            op.ApprovedBy = input.ActorEmail;
        }

        await _db.SaveChangesAsync(cancellationToken);

        _logger.LogInformation(
            "Approved {Count} archive operations for site {SiteId} by {Actor}",
            siteOperations.Count, input.SiteId, input.ActorEmail);

        // Signal the orchestrator
        await RaiseApprovalEventAsync(input, cancellationToken);

        return new ApprovalActionResult
        {
            Success = true,
            Action = "approve",
            Message = $"Approved archiving of {siteOperations.Count} files. Archive operation will proceed.",
        };
    }

    private async Task<ApprovalActionResult> HandleRejectAsync(
        Guid tenantId, ApprovalActionInput input, CancellationToken cancellationToken)
    {
        var now = DateTimeOffset.UtcNow;

        // Update matching operations to Vetoed
        var siteOperations = await _db.ArchiveOperations
            .Include(o => o.FileMetadata)
            .Where(o => o.ClientTenantId == tenantId
                && o.Status == "AwaitingApproval"
                && o.FileMetadata.SiteId == input.SiteId)
            .ToListAsync(cancellationToken);

        foreach (var op in siteOperations)
        {
            op.Status = "Vetoed";
            op.VetoedBy = input.ActorEmail;
            op.VetoReason = input.Reason;
            op.VetoedAt = now;
        }

        await _db.SaveChangesAsync(cancellationToken);

        _logger.LogInformation(
            "Vetoed {Count} archive operations for site {SiteId} by {Actor}. Reason: {Reason}",
            siteOperations.Count, input.SiteId, input.ActorEmail, input.Reason ?? "(none)");

        // Signal the orchestrator with rejection
        await RaiseApprovalEventAsync(input, cancellationToken);

        return new ApprovalActionResult
        {
            Success = true,
            Action = "reject",
            Message = $"Rejected archiving of {siteOperations.Count} files. Files will remain in their current location.",
        };
    }

    private async Task<ApprovalActionResult> HandleReviewAsync(
        Guid tenantId, ApprovalActionInput input, CancellationToken cancellationToken)
    {
        // Update matching operations to ReviewRequested
        var siteOperations = await _db.ArchiveOperations
            .Include(o => o.FileMetadata)
            .Where(o => o.ClientTenantId == tenantId
                && o.Status == "AwaitingApproval"
                && o.FileMetadata.SiteId == input.SiteId)
            .ToListAsync(cancellationToken);

        foreach (var op in siteOperations)
        {
            op.Status = "ReviewRequested";
        }

        // Flag the tenant for dashboard review
        var tenant = await _db.ClientTenants
            .FirstOrDefaultAsync(t => t.Id == tenantId, cancellationToken);

        if (tenant is not null)
        {
            tenant.ReviewFlagged = true;
        }

        await _db.SaveChangesAsync(cancellationToken);

        _logger.LogInformation(
            "Review requested for {Count} archive operations for site {SiteId} by {Actor}. Tenant flagged for review.",
            siteOperations.Count, input.SiteId, input.ActorEmail);

        // Signal the orchestrator with review request
        await RaiseApprovalEventAsync(input, cancellationToken);

        return new ApprovalActionResult
        {
            Success = true,
            Action = "review",
            Message = $"Review requested for {siteOperations.Count} files. An administrator will review this request.",
        };
    }

    private async Task RaiseApprovalEventAsync(
        ApprovalActionInput input, CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(input.OrchestrationInstanceId))
        {
            _logger.LogWarning(
                "No orchestration instance ID provided — cannot signal orchestrator for {Action} on site {SiteId}",
                input.Action, input.SiteId);
            return;
        }

        var eventData = new ApprovalEventData
        {
            Action = input.Action.ToLowerInvariant(),
            SiteId = input.SiteId,
            ActorEmail = input.ActorEmail,
            Reason = input.Reason,
            Timestamp = DateTimeOffset.UtcNow,
        };

        var eventName = $"ApprovalReceived_{input.SiteId}";

        await _durableClient.RaiseEventAsync(
            input.OrchestrationInstanceId,
            eventName,
            eventData,
            cancellationToken);

        _logger.LogInformation(
            "Raised event {EventName} on orchestration {OrchestrationId} for {Action}",
            eventName, input.OrchestrationInstanceId, input.Action);
    }
}
