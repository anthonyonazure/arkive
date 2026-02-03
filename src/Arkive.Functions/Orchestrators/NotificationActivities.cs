using Arkive.Core.DTOs;
using Arkive.Core.Interfaces;
using Arkive.Data;
using Microsoft.Azure.Functions.Worker;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Arkive.Functions.Orchestrators;

public class NotificationActivities
{
    private readonly ITeamsNotificationService _notificationService;
    private readonly ArkiveDbContext _db;
    private readonly ILogger<NotificationActivities> _logger;

    public NotificationActivities(
        ITeamsNotificationService notificationService,
        ArkiveDbContext db,
        ILogger<NotificationActivities> logger)
    {
        _notificationService = notificationService;
        _db = db;
        _logger = logger;
    }

    /// <summary>
    /// Groups files by site owner for sending per-owner approval cards.
    /// Resolves site display names and owner AAD IDs from the database.
    /// </summary>
    [Function(nameof(GroupFilesBySiteOwner))]
    public async Task<List<SiteOwnerFileGroup>> GroupFilesBySiteOwner(
        [ActivityTrigger] GroupFilesBySiteOwnerInput input)
    {
        _logger.LogInformation(
            "Grouping {FileCount} files by site owner for tenant {TenantId}",
            input.Files.Count, input.TenantId);

        // Look up site display names
        var siteIds = input.Files.Select(f => f.SiteId).Distinct().ToList();
        var sites = await _db.SharePointSites
            .AsNoTracking()
            .Where(s => s.ClientTenantId == input.TenantId && siteIds.Contains(s.SiteId))
            .Select(s => new { s.SiteId, s.DisplayName })
            .ToListAsync();

        var siteNameMap = sites.ToDictionary(s => s.SiteId, s => s.DisplayName);

        // Group files by (SiteId, Owner) — each unique site+owner pair gets a card
        var groups = input.Files
            .Where(f => !string.IsNullOrEmpty(f.Owner))
            .GroupBy(f => new { f.SiteId, f.Owner })
            .Select(g => new SiteOwnerFileGroup
            {
                SiteId = g.Key.SiteId,
                SiteName = siteNameMap.GetValueOrDefault(g.Key.SiteId, g.Key.SiteId),
                OwnerEmail = g.Key.Owner!,
                // AAD ID will be resolved by the notification service or passed from Graph API
                // For now, use email as identifier — TeamsNotificationService handles resolution
                OwnerAadId = string.Empty,
                FileCount = g.Count(),
                TotalSizeBytes = g.Sum(f => f.SizeBytes),
                TargetTier = g.First().TargetTier,
            })
            .ToList();

        _logger.LogInformation(
            "Grouped into {GroupCount} site-owner groups for tenant {TenantId}",
            groups.Count, input.TenantId);

        return groups;
    }

    /// <summary>
    /// Transitions ArchiveOperation records for delivered sites to "AwaitingApproval" status.
    /// Called after notifications are sent so the ApprovalActionHandler can find and update them.
    /// </summary>
    [Function(nameof(SetOperationsAwaitingApproval))]
    public async Task SetOperationsAwaitingApproval(
        [ActivityTrigger] SetAwaitingApprovalInput input)
    {
        var operations = await _db.ArchiveOperations
            .Include(o => o.FileMetadata)
            .Where(o => o.ClientTenantId == input.TenantId
                && (o.Status == "Pending" || o.Status == "InProgress")
                && input.SiteIds.Contains(o.FileMetadata.SiteId))
            .ToListAsync();

        foreach (var op in operations)
        {
            op.Status = "AwaitingApproval";
        }

        await _db.SaveChangesAsync();

        _logger.LogInformation(
            "Set {Count} operations to AwaitingApproval for tenant {TenantId} across {SiteCount} sites",
            operations.Count, input.TenantId, input.SiteIds.Count);
    }

    /// <summary>
    /// Retrieves the tenant's AutoApprovalDays setting from the database.
    /// Returns null if auto-approval is disabled, 0 for immediate approval, or 1-365 for days.
    /// </summary>
    [Function(nameof(GetTenantAutoApprovalDays))]
    public async Task<int?> GetTenantAutoApprovalDays(
        [ActivityTrigger] GetAutoApprovalDaysInput input)
    {
        var tenant = await _db.ClientTenants
            .AsNoTracking()
            .Where(t => t.Id == input.TenantId)
            .Select(t => new { t.AutoApprovalDays })
            .FirstOrDefaultAsync()
            ?? throw new InvalidOperationException(
                $"Tenant {input.TenantId} not found when retrieving auto-approval setting.");

        _logger.LogInformation(
            "Tenant {TenantId} auto-approval setting: {AutoApprovalDays} days",
            input.TenantId, tenant.AutoApprovalDays?.ToString() ?? "disabled");

        return tenant.AutoApprovalDays;
    }

    /// <summary>
    /// Auto-approves all AwaitingApproval operations for a specific site when the approval timer expires.
    /// Sets ApprovedBy to "System:AutoApproval" to distinguish from manual approvals.
    /// </summary>
    [Function(nameof(AutoApproveExpiredOperations))]
    public async Task<int> AutoApproveExpiredOperations(
        [ActivityTrigger] AutoApproveExpiredInput input)
    {
        var operations = await _db.ArchiveOperations
            .Include(o => o.FileMetadata)
            .Where(o => o.ClientTenantId == input.TenantId
                && o.Status == "AwaitingApproval"
                && o.FileMetadata.SiteId == input.SiteId)
            .ToListAsync();

        foreach (var op in operations)
        {
            op.Status = "Approved";
            op.ApprovedBy = "System:AutoApproval";
        }

        await _db.SaveChangesAsync();

        _logger.LogInformation(
            "Auto-approved {Count} operations for site {SiteId} in tenant {TenantId} (timer expired)",
            operations.Count, input.SiteId, input.TenantId);

        return operations.Count;
    }

    /// <summary>
    /// Sends an approval Adaptive Card to a single site owner via Teams.
    /// </summary>
    [Function(nameof(SendApprovalCard))]
    public async Task<ApprovalNotificationResult> SendApprovalCard(
        [ActivityTrigger] ApprovalNotificationInput input)
    {
        _logger.LogInformation(
            "Sending approval card to {SiteOwner} for site {SiteName} ({FileCount} files)",
            input.SiteOwnerEmail, input.SiteName, input.FileCount);

        return await _notificationService.SendApprovalCardAsync(input);
    }
}
