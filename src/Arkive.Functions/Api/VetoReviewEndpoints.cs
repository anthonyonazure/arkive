using Arkive.Core.DTOs;
using Arkive.Core.Models;
using Arkive.Data;
using Arkive.Functions.Extensions;
using Arkive.Functions.Middleware;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Arkive.Functions.Api;

public class VetoReviewEndpoints
{
    private readonly ArkiveDbContext _db;
    private readonly ILogger<VetoReviewEndpoints> _logger;

    public VetoReviewEndpoints(ArkiveDbContext db, ILogger<VetoReviewEndpoints> logger)
    {
        _db = db;
        _logger = logger;
    }

    [Function("GetVetoReviews")]
    public async Task<IActionResult> GetVetoReviews(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "v1/tenants/{tenantId}/veto-reviews")] HttpRequest req,
        string tenantId,
        FunctionContext context)
    {
        var mspOrgId = context.GetMspOrgId();
        if (string.IsNullOrEmpty(mspOrgId))
            return ResponseEnvelopeHelper.Unauthorized(context.InvocationId);

        if (!Guid.TryParse(tenantId, out var parsedTenantId) || !Guid.TryParse(mspOrgId, out var parsedOrgId))
            return ResponseEnvelopeHelper.BadRequest("Invalid tenant or organization ID.", context.InvocationId);

        // Verify tenant belongs to org
        var tenantExists = await _db.ClientTenants
            .AnyAsync(t => t.Id == parsedTenantId && t.MspOrgId == parsedOrgId, req.HttpContext.RequestAborted);
        if (!tenantExists)
            return ResponseEnvelopeHelper.NotFound("Tenant not found.", context.InvocationId);

        // Get vetoed operations with file metadata
        var vetoedOps = await _db.ArchiveOperations
            .AsNoTracking()
            .Include(o => o.FileMetadata)
            .Where(o => o.ClientTenantId == parsedTenantId && o.Status == "Vetoed" && o.FileMetadata != null)
            .OrderByDescending(o => o.VetoedAt)
            .Take(200)
            .ToListAsync(req.HttpContext.RequestAborted);

        // Look up site display names
        var siteIds = vetoedOps.Select(o => o.FileMetadata.SiteId).Distinct().ToList();
        var siteNameMap = await _db.SharePointSites
            .AsNoTracking()
            .Where(s => s.ClientTenantId == parsedTenantId && siteIds.Contains(s.SiteId))
            .ToDictionaryAsync(s => s.SiteId, s => s.DisplayName, req.HttpContext.RequestAborted);

        var reviews = vetoedOps.Select(o => new VetoReviewDto
        {
            OperationId = o.Id,
            FileMetadataId = o.FileMetadataId,
            FileName = o.FileMetadata.FileName,
            FilePath = o.FileMetadata.FilePath,
            SiteId = o.FileMetadata.SiteId,
            SiteName = siteNameMap.GetValueOrDefault(o.FileMetadata.SiteId, o.FileMetadata.SiteId),
            SizeBytes = o.FileMetadata.SizeBytes,
            VetoedBy = o.VetoedBy ?? string.Empty,
            VetoReason = o.VetoReason,
            VetoedAt = o.VetoedAt,
        }).ToList();

        return ResponseEnvelopeHelper.Ok(reviews);
    }

    [Function("ResolveVeto")]
    public async Task<IActionResult> ResolveVeto(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "v1/tenants/{tenantId}/veto-reviews/{operationId}/resolve")] HttpRequest req,
        string tenantId,
        string operationId,
        FunctionContext context)
    {
        var mspOrgId = context.GetMspOrgId();
        if (string.IsNullOrEmpty(mspOrgId))
            return ResponseEnvelopeHelper.Unauthorized(context.InvocationId);

        if (!Guid.TryParse(tenantId, out var parsedTenantId) || !Guid.TryParse(mspOrgId, out var parsedOrgId))
            return ResponseEnvelopeHelper.BadRequest("Invalid tenant or organization ID.", context.InvocationId);

        if (!Guid.TryParse(operationId, out var parsedOperationId))
            return ResponseEnvelopeHelper.BadRequest("Invalid operation ID.", context.InvocationId);

        var body = await req.ReadFromJsonAsync<VetoActionRequest>(req.HttpContext.RequestAborted);
        if (body is null || string.IsNullOrWhiteSpace(body.Action))
            return ResponseEnvelopeHelper.BadRequest("Action is required.", context.InvocationId);

        // Load operation with file metadata, verifying tenant ownership
        var operation = await _db.ArchiveOperations
            .Include(o => o.FileMetadata)
            .FirstOrDefaultAsync(
                o => o.Id == parsedOperationId
                    && o.ClientTenantId == parsedTenantId
                    && o.MspOrgId == parsedOrgId
                    && o.Status == "Vetoed",
                req.HttpContext.RequestAborted);

        if (operation is null)
            return ResponseEnvelopeHelper.NotFound("Vetoed operation not found.", context.InvocationId);

        var result = body.Action.ToLowerInvariant() switch
        {
            "accept" => await HandleAcceptVeto(operation),
            "override" => await HandleOverrideVeto(operation, parsedTenantId),
            "exclude" => await HandleExcludeLibrary(operation, parsedTenantId, parsedOrgId),
            _ => new VetoActionResult
            {
                Success = false,
                Action = body.Action,
                Message = $"Unknown action: {body.Action}. Valid actions: accept, override, exclude.",
            },
        };

        if (result.Success)
        {
            // Check if tenant has any remaining vetoed operations
            var remainingVetos = await _db.ArchiveOperations
                .CountAsync(o => o.ClientTenantId == parsedTenantId && o.Status == "Vetoed",
                    req.HttpContext.RequestAborted);

            // Clear ReviewFlagged if no more vetos
            if (remainingVetos == 0)
            {
                var tenant = await _db.ClientTenants
                    .FirstOrDefaultAsync(t => t.Id == parsedTenantId, req.HttpContext.RequestAborted);
                if (tenant is not null && tenant.ReviewFlagged)
                {
                    tenant.ReviewFlagged = false;
                    await _db.SaveChangesAsync(req.HttpContext.RequestAborted);
                }
            }
        }

        _logger.LogInformation(
            "Veto resolution for operation {OperationId}: {Action} — {Success}, {Message}",
            parsedOperationId, body.Action, result.Success, result.Message);

        return result.Success
            ? ResponseEnvelopeHelper.Ok(result)
            : ResponseEnvelopeHelper.BadRequest(result.Message, context.InvocationId);
    }

    private async Task<VetoActionResult> HandleAcceptVeto(ArchiveOperation operation)
    {
        // Accept veto = keep file in SharePoint, revert to Active
        operation.Status = "VetoAccepted";
        operation.FileMetadata.ArchiveStatus = "Active";

        await _db.SaveChangesAsync();

        return new VetoActionResult
        {
            Success = true,
            Action = "accept",
            Message = $"Veto accepted for {operation.FileMetadata.FileName}. File will remain in SharePoint.",
        };
    }

    private async Task<VetoActionResult> HandleOverrideVeto(ArchiveOperation operation, Guid tenantId)
    {
        // Override veto = re-queue for archiving (create new operation bypassing approval)
        operation.Status = "VetoOverridden";
        operation.FileMetadata.ArchiveStatus = "PendingArchive";

        var newOperation = new ArchiveOperation
        {
            ClientTenantId = tenantId,
            MspOrgId = operation.MspOrgId,
            FileMetadataId = operation.FileMetadataId,
            OperationId = $"override-{operation.Id}-{DateTimeOffset.UtcNow.Ticks}",
            Action = "Archive",
            SourcePath = operation.SourcePath,
            DestinationPath = operation.DestinationPath,
            TargetTier = operation.TargetTier,
            Status = "Approved",
            ApprovedBy = "System:VetoOverride",
        };

        _db.ArchiveOperations.Add(newOperation);
        await _db.SaveChangesAsync();

        return new VetoActionResult
        {
            Success = true,
            Action = "override",
            Message = $"Veto overridden for {operation.FileMetadata.FileName}. File re-queued for archiving.",
        };
    }

    private async Task<VetoActionResult> HandleExcludeLibrary(
        ArchiveOperation operation, Guid tenantId, Guid mspOrgId)
    {
        // Exclude library = create exclusion rule for the library path containing this file
        var filePath = operation.FileMetadata.FilePath;
        var libraryPath = ExtractLibraryPath(filePath);

        if (string.IsNullOrEmpty(libraryPath))
        {
            return new VetoActionResult
            {
                Success = false,
                Action = "exclude",
                Message = "Cannot determine library path from file location. Use 'Accept Veto' instead.",
            };
        }

        operation.Status = "VetoAccepted";
        operation.FileMetadata.ArchiveStatus = "Active";

        // Create exclusion rule for the library
        var exclusionRule = new ArchiveRule
        {
            ClientTenantId = tenantId,
            MspOrgId = mspOrgId,
            Name = $"Excluded: {libraryPath}",
            RuleType = "exclusion",
            Criteria = System.Text.Json.JsonSerializer.Serialize(new { libraryPath }),
            TargetTier = "Cool",
            IsActive = true,
            CreatedBy = "System:VetoExclusion",
        };

        _db.ArchiveRules.Add(exclusionRule);

        // Also accept all other vetoed operations in the same library
        var relatedOps = await _db.ArchiveOperations
            .Include(o => o.FileMetadata)
            .Where(o => o.ClientTenantId == tenantId
                && o.Status == "Vetoed"
                && o.Id != operation.Id
                && o.FileMetadata.FilePath.StartsWith(libraryPath + "/"))
            .ToListAsync();

        foreach (var op in relatedOps)
        {
            op.Status = "VetoAccepted";
            op.FileMetadata.ArchiveStatus = "Active";
        }

        await _db.SaveChangesAsync();

        var totalResolved = 1 + relatedOps.Count;

        return new VetoActionResult
        {
            Success = true,
            Action = "exclude",
            Message = $"Exclusion rule created for library '{libraryPath}'. {totalResolved} veto(s) resolved.",
            ExclusionRuleId = exclusionRule.Id,
        };
    }

    /// <summary>
    /// Extracts the top-level folder (document library root) from a relative file path.
    /// e.g., "Shared Documents/Reports/Q1/report.xlsx" → "Shared Documents"
    /// e.g., "/Shared Documents/Reports/Q1/report.xlsx" → "Shared Documents"
    /// e.g., "report.xlsx" → "" (file in root)
    /// </summary>
    private static string ExtractLibraryPath(string filePath)
    {
        if (string.IsNullOrEmpty(filePath))
            return string.Empty;

        var trimmed = filePath.TrimStart('/');
        var firstSlash = trimmed.IndexOf('/');
        return firstSlash > 0 ? trimmed[..firstSlash] : string.Empty;
    }
}
