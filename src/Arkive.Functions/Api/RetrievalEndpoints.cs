using Arkive.Core.DTOs;
using Arkive.Core.Interfaces;
using Arkive.Core.Models;
using Arkive.Data;
using Arkive.Functions.Extensions;
using Arkive.Functions.Middleware;
using Arkive.Functions.Orchestrators;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using Microsoft.DurableTask;
using Microsoft.DurableTask.Client;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Arkive.Functions.Api;

public class RetrievalEndpoints
{
    private readonly ArkiveDbContext _db;
    private readonly IArchiveService _archiveService;
    private readonly ILogger<RetrievalEndpoints> _logger;

    public RetrievalEndpoints(ArkiveDbContext db, IArchiveService archiveService, ILogger<RetrievalEndpoints> logger)
    {
        _db = db;
        _archiveService = archiveService;
        _logger = logger;
    }

    [Function("SearchArchivedFiles")]
    public async Task<IActionResult> SearchArchivedFiles(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "v1/archive/search")] HttpRequest req,
        FunctionContext context)
    {
        var mspOrgId = context.GetMspOrgId();
        if (string.IsNullOrEmpty(mspOrgId))
            return ResponseEnvelopeHelper.Unauthorized(context.InvocationId);

        if (!Guid.TryParse(mspOrgId, out var parsedOrgId))
            return ResponseEnvelopeHelper.BadRequest("Invalid organization ID.", context.InvocationId);

        // Parse query parameters
        var query = req.Query;
        var searchQuery = query["q"].FirstOrDefault()?.Trim();
        var tenantIdFilter = query["tenantId"].FirstOrDefault();
        var fileType = query["fileType"].FirstOrDefault();
        var tierFilter = query["tier"].FirstOrDefault();

        int.TryParse(query["page"], out var page);
        int.TryParse(query["pageSize"], out var pageSize);
        if (page < 1) page = 1;
        if (pageSize < 1 || pageSize > 100) pageSize = 25;

        var sortBy = query["sortBy"].FirstOrDefault();
        var sortDir = query["sortDir"].FirstOrDefault();

        try
        {
            // Get tenant IDs belonging to this org
            var tenantIds = await _db.ClientTenants
                .AsNoTracking()
                .Where(t => t.MspOrgId == parsedOrgId)
                .Select(t => t.Id)
                .ToListAsync(req.HttpContext.RequestAborted);

            if (tenantIds.Count == 0)
                return ResponseEnvelopeHelper.Ok(new ArchiveSearchResultDto
                {
                    Page = page, PageSize = pageSize, TotalCount = 0, TotalPages = 0,
                });

            // Build base query: archived files across all org tenants
            var fileQuery = _db.FileMetadata
                .AsNoTracking()
                .Where(f => tenantIds.Contains(f.ClientTenantId) && f.ArchiveStatus == "Archived");

            // Filter by specific tenant (validate it belongs to the org)
            if (!string.IsNullOrEmpty(tenantIdFilter) && Guid.TryParse(tenantIdFilter, out var parsedTenantFilter))
            {
                if (!tenantIds.Contains(parsedTenantFilter))
                    return ResponseEnvelopeHelper.BadRequest("Tenant not found in your organization.", context.InvocationId);
                fileQuery = fileQuery.Where(f => f.ClientTenantId == parsedTenantFilter);
            }

            // Text search: match file name or file path (minimum 2 chars to avoid full-table scans)
            if (!string.IsNullOrEmpty(searchQuery))
            {
                if (searchQuery.Length < 2)
                    return ResponseEnvelopeHelper.BadRequest("Search query must be at least 2 characters.", context.InvocationId);
                fileQuery = fileQuery.Where(f =>
                    f.FileName.Contains(searchQuery) || f.FilePath.Contains(searchQuery));
            }

            // File type filter
            if (!string.IsNullOrEmpty(fileType))
            {
                fileQuery = fileQuery.Where(f => f.FileType == fileType);
            }

            // Blob tier filter
            if (!string.IsNullOrEmpty(tierFilter))
            {
                fileQuery = fileQuery.Where(f => f.BlobTier == tierFilter);
            }

            // Get total count
            var totalCount = await fileQuery.CountAsync(req.HttpContext.RequestAborted);

            // Apply sorting
            var sortAsc = string.Equals(sortDir, "asc", StringComparison.OrdinalIgnoreCase);
            fileQuery = sortBy?.ToLowerInvariant() switch
            {
                "name" => sortAsc ? fileQuery.OrderBy(f => f.FileName) : fileQuery.OrderByDescending(f => f.FileName),
                "size" => sortAsc ? fileQuery.OrderBy(f => f.SizeBytes) : fileQuery.OrderByDescending(f => f.SizeBytes),
                "type" => sortAsc ? fileQuery.OrderBy(f => f.FileType) : fileQuery.OrderByDescending(f => f.FileType),
                "date" => sortAsc ? fileQuery.OrderBy(f => f.ScannedAt) : fileQuery.OrderByDescending(f => f.ScannedAt),
                _ => fileQuery.OrderByDescending(f => f.SizeBytes),
            };

            // Paginate
            var files = await fileQuery
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .Select(f => new
                {
                    f.Id,
                    f.FileName,
                    f.FilePath,
                    f.FileType,
                    f.SizeBytes,
                    f.Owner,
                    f.SiteId,
                    f.ClientTenantId,
                    f.BlobTier,
                    f.ScannedAt,
                    f.LastModifiedAt,
                })
                .ToListAsync(req.HttpContext.RequestAborted);

            // Look up tenant and site display names
            var fileTenantIds = files.Select(f => f.ClientTenantId).Distinct().ToList();
            var tenantNameMap = await _db.ClientTenants
                .AsNoTracking()
                .Where(t => fileTenantIds.Contains(t.Id))
                .ToDictionaryAsync(t => t.Id, t => t.DisplayName, req.HttpContext.RequestAborted);

            var fileSiteIds = files.Select(f => f.SiteId).Distinct().ToList();
            var siteNameList = await _db.SharePointSites
                .AsNoTracking()
                .Where(s => fileTenantIds.Contains(s.ClientTenantId) && fileSiteIds.Contains(s.SiteId))
                .Select(s => new { s.SiteId, s.DisplayName })
                .ToListAsync(req.HttpContext.RequestAborted);
            var siteNameMap = siteNameList
                .GroupBy(s => s.SiteId)
                .ToDictionary(g => g.Key, g => g.First().DisplayName);

            var results = files.Select(f => new ArchivedFileDto
            {
                FileMetadataId = f.Id,
                FileName = f.FileName,
                FilePath = f.FilePath,
                FileType = f.FileType,
                SizeBytes = f.SizeBytes,
                Owner = f.Owner,
                SiteId = f.SiteId,
                SiteName = siteNameMap.GetValueOrDefault(f.SiteId, f.SiteId),
                TenantId = f.ClientTenantId,
                TenantName = tenantNameMap.GetValueOrDefault(f.ClientTenantId, string.Empty),
                BlobTier = f.BlobTier ?? "Cool",
                EstimatedRetrievalTime = EstimateRetrievalTime(f.BlobTier),
                ArchivedAt = f.ScannedAt,
                LastModifiedAt = f.LastModifiedAt,
            }).ToList();

            return ResponseEnvelopeHelper.Ok(new ArchiveSearchResultDto
            {
                Files = results,
                Page = page,
                PageSize = pageSize,
                TotalCount = totalCount,
                TotalPages = (int)Math.Ceiling(totalCount / (double)pageSize),
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to search archived files for org {MspOrgId}", parsedOrgId);
            return ResponseEnvelopeHelper.Error(
                System.Net.HttpStatusCode.InternalServerError,
                "INTERNAL_ERROR",
                "Failed to search archived files.",
                context.InvocationId);
        }
    }

    [Function("TriggerRetrieval")]
    public async Task<IActionResult> TriggerRetrieval(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "v1/archive/retrieve")] HttpRequest req,
        [DurableClient] DurableTaskClient durableClient,
        FunctionContext context)
    {
        var mspOrgId = context.GetMspOrgId();
        if (string.IsNullOrEmpty(mspOrgId))
            return ResponseEnvelopeHelper.Unauthorized(context.InvocationId);

        if (!Guid.TryParse(mspOrgId, out var parsedOrgId))
            return ResponseEnvelopeHelper.BadRequest("Invalid organization ID.", context.InvocationId);

        RetrievalRequest? request;
        try
        {
            request = await req.ReadFromJsonAsync<RetrievalRequest>(req.HttpContext.RequestAborted);
        }
        catch
        {
            return ResponseEnvelopeHelper.BadRequest("Invalid request body.", context.InvocationId);
        }

        if (request is null || request.FileIds.Count == 0)
            return ResponseEnvelopeHelper.BadRequest("At least one file ID is required.", context.InvocationId);

        if (request.FileIds.Count > 10)
            return ResponseEnvelopeHelper.BadRequest("Maximum 10 files per retrieval batch.", context.InvocationId);

        try
        {
            // Get org tenants for authorization
            var orgTenantIds = await _db.ClientTenants
                .AsNoTracking()
                .Where(t => t.MspOrgId == parsedOrgId)
                .Select(t => t.Id)
                .ToListAsync(req.HttpContext.RequestAborted);

            // Load requested files — only archived files belonging to this org
            var files = await _db.FileMetadata
                .AsNoTracking()
                .Where(f => request.FileIds.Contains(f.Id)
                    && orgTenantIds.Contains(f.ClientTenantId)
                    && f.ArchiveStatus == "Archived")
                .ToListAsync(req.HttpContext.RequestAborted);

            // Get M365 tenant IDs for Graph API calls
            var tenantIdSet = files.Select(f => f.ClientTenantId).Distinct().ToList();
            var tenantMap = await _db.ClientTenants
                .AsNoTracking()
                .Where(t => tenantIdSet.Contains(t.Id))
                .ToDictionaryAsync(t => t.Id, t => t.M365TenantId, req.HttpContext.RequestAborted);

            var skipped = request.FileIds.Count - files.Count;
            var operations = new List<RetrievalOperationDto>();

            // Separate Cool/Cold (immediate) from Archive (rehydration required)
            var coolColdFiles = files.Where(f =>
                f.BlobTier is null or "Cool" or "Cold").ToList();
            var archiveFiles = files.Where(f =>
                f.BlobTier == "Archive").ToList();

            // Process Cool/Cold files immediately
            foreach (var file in coolColdFiles)
            {
                if (!tenantMap.TryGetValue(file.ClientTenantId, out var m365TenantId))
                    continue;

                var result = await _archiveService.RetrieveFileAsync(new RetrieveFileInput
                {
                    TenantId = file.ClientTenantId,
                    MspOrgId = parsedOrgId,
                    M365TenantId = m365TenantId,
                    FileMetadataId = file.Id,
                    SiteId = file.SiteId,
                    DriveId = file.DriveId,
                    ItemId = file.ItemId,
                    FileName = file.FileName,
                    FilePath = file.FilePath,
                    SizeBytes = file.SizeBytes,
                    BlobTier = file.BlobTier,
                }, req.HttpContext.RequestAborted);

                operations.Add(result);
            }

            // Process Archive-tier files via rehydration orchestrator
            foreach (var file in archiveFiles)
            {
                if (!tenantMap.TryGetValue(file.ClientTenantId, out var m365TenantId))
                    continue;

                var operationId = $"rehydrate-{file.Id}-{DateTimeOffset.UtcNow.Ticks}";

                // Create a tracking operation record
                var operation = new ArchiveOperation
                {
                    ClientTenantId = file.ClientTenantId,
                    MspOrgId = parsedOrgId,
                    FileMetadataId = file.Id,
                    OperationId = operationId,
                    Action = "Retrieve",
                    SourcePath = $"tenant-{file.ClientTenantId}/{file.FilePath.TrimStart('/')}",
                    DestinationPath = file.FilePath,
                    TargetTier = "Cool",
                    Status = "Rehydrating",
                };
                _db.ArchiveOperations.Add(operation);
                await _db.SaveChangesAsync(req.HttpContext.RequestAborted);

                // Schedule rehydration orchestrator (skip if already running for this file)
                var instanceId = $"rehydrate-{file.Id}";
                var existingInstance = await durableClient.GetInstanceAsync(instanceId, req.HttpContext.RequestAborted);
                if (existingInstance is not null &&
                    existingInstance.RuntimeStatus is OrchestrationRuntimeStatus.Running or OrchestrationRuntimeStatus.Pending)
                {
                    _logger.LogInformation(
                        "Rehydration already in progress for file {FileName} ({InstanceId}), skipping duplicate",
                        file.FileName, instanceId);
                    operations.Add(new RetrievalOperationDto
                    {
                        Id = operation.Id,
                        FileMetadataId = file.Id,
                        FileName = file.FileName,
                        FilePath = file.FilePath,
                        SizeBytes = file.SizeBytes,
                        BlobTier = "Archive",
                        Status = "Rehydrating",
                        CreatedAt = operation.CreatedAt,
                    });
                    continue;
                }

                await durableClient.ScheduleNewOrchestrationInstanceAsync(
                    nameof(RehydrationOrchestrator),
                    new RehydrationInput
                    {
                        TenantId = file.ClientTenantId,
                        MspOrgId = parsedOrgId,
                        M365TenantId = m365TenantId,
                        FileMetadataId = file.Id,
                        SiteId = file.SiteId,
                        DriveId = file.DriveId,
                        ItemId = file.ItemId,
                        FileName = file.FileName,
                        FilePath = file.FilePath,
                        SizeBytes = file.SizeBytes,
                        OperationId = operationId,
                    },
                    new StartOrchestrationOptions { InstanceId = instanceId },
                    req.HttpContext.RequestAborted);

                operations.Add(new RetrievalOperationDto
                {
                    Id = operation.Id,
                    FileMetadataId = file.Id,
                    FileName = file.FileName,
                    FilePath = file.FilePath,
                    SizeBytes = file.SizeBytes,
                    BlobTier = "Archive",
                    Status = "Rehydrating",
                    CreatedAt = operation.CreatedAt,
                });

                _logger.LogInformation(
                    "Scheduled rehydration orchestration {InstanceId} for file {FileName}",
                    instanceId, file.FileName);
            }

            var queued = operations.Count(o => o.Status is "InProgress" or "Completed" or "Rehydrating");
            var message = $"Retrieval initiated for {queued} file(s).";
            if (archiveFiles.Count > 0)
                message += $" {archiveFiles.Count} archive-tier file(s) queued for rehydration (4-6 hours).";
            if (skipped > 0)
                message += $" {skipped} file(s) not found or not eligible.";

            return ResponseEnvelopeHelper.Ok(new RetrievalBatchResult
            {
                TotalFiles = request.FileIds.Count,
                Queued = queued,
                Skipped = skipped,
                Message = message,
                Operations = operations,
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to trigger retrieval for org {MspOrgId}", parsedOrgId);
            return ResponseEnvelopeHelper.Error(
                System.Net.HttpStatusCode.InternalServerError,
                "INTERNAL_ERROR",
                "Failed to trigger file retrieval.",
                context.InvocationId);
        }
    }

    [Function("GetRetrievalJobs")]
    public async Task<IActionResult> GetRetrievalJobs(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "v1/archive/retrievals")] HttpRequest req,
        FunctionContext context)
    {
        var mspOrgId = context.GetMspOrgId();
        if (string.IsNullOrEmpty(mspOrgId))
            return ResponseEnvelopeHelper.Unauthorized(context.InvocationId);

        if (!Guid.TryParse(mspOrgId, out var parsedOrgId))
            return ResponseEnvelopeHelper.BadRequest("Invalid organization ID.", context.InvocationId);

        var statusFilter = req.Query["status"].FirstOrDefault();

        try
        {
            var orgTenantIds = await _db.ClientTenants
                .AsNoTracking()
                .Where(t => t.MspOrgId == parsedOrgId)
                .Select(t => t.Id)
                .ToListAsync(req.HttpContext.RequestAborted);

            var query = _db.ArchiveOperations
                .AsNoTracking()
                .Include(o => o.FileMetadata)
                .Where(o => orgTenantIds.Contains(o.ClientTenantId) && o.Action == "Retrieve");

            if (!string.IsNullOrEmpty(statusFilter))
                query = query.Where(o => o.Status == statusFilter);

            var operations = await query
                .OrderByDescending(o => o.CreatedAt)
                .Take(100)
                .Select(o => new RetrievalOperationDto
                {
                    Id = o.Id,
                    FileMetadataId = o.FileMetadataId,
                    FileName = o.FileMetadata != null ? o.FileMetadata.FileName : string.Empty,
                    FilePath = o.FileMetadata != null ? o.FileMetadata.FilePath : string.Empty,
                    SizeBytes = o.FileMetadata != null ? o.FileMetadata.SizeBytes : 0,
                    BlobTier = o.TargetTier,
                    Status = o.Status,
                    ErrorMessage = o.ErrorMessage,
                    CreatedAt = o.CreatedAt,
                    CompletedAt = o.CompletedAt,
                })
                .ToListAsync(req.HttpContext.RequestAborted);

            return ResponseEnvelopeHelper.Ok(operations);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get retrieval jobs for org {MspOrgId}", parsedOrgId);
            return ResponseEnvelopeHelper.Error(
                System.Net.HttpStatusCode.InternalServerError,
                "INTERNAL_ERROR",
                "Failed to get retrieval jobs.",
                context.InvocationId);
        }
    }

    private static string EstimateRetrievalTime(string? blobTier) => blobTier?.ToLowerInvariant() switch
    {
        "cool" => "< 10 minutes",
        "cold" => "< 10 minutes",
        "archive" => "4-6 hours",
        _ => "< 10 minutes",
    };
}
