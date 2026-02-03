using System.Text;
using Arkive.Core.DTOs;
using Arkive.Data;
using Arkive.Functions.Extensions;
using Arkive.Functions.Middleware;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Arkive.Functions.Api;

public class AuditEndpoints
{
    private readonly ArkiveDbContext _db;
    private readonly ILogger<AuditEndpoints> _logger;

    public AuditEndpoints(ArkiveDbContext db, ILogger<AuditEndpoints> logger)
    {
        _db = db;
        _logger = logger;
    }

    [Function("GetAuditEntries")]
    public async Task<IActionResult> GetAuditEntries(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "v1/audit")] HttpRequest req,
        FunctionContext context)
    {
        var mspOrgId = context.GetMspOrgId();
        if (string.IsNullOrEmpty(mspOrgId))
            return ResponseEnvelopeHelper.Unauthorized(context.InvocationId);

        if (!Guid.TryParse(mspOrgId, out var parsedOrgId))
            return ResponseEnvelopeHelper.BadRequest("Invalid organization ID.", context.InvocationId);

        // Parse query filters
        var query = req.Query;
        var tenantIdFilter = query["tenantId"].FirstOrDefault();
        var actionFilter = query["action"].FirstOrDefault();
        var actorFilter = query["actor"].FirstOrDefault();
        var fromDate = query["from"].FirstOrDefault();
        var toDate = query["to"].FirstOrDefault();

        int.TryParse(query["page"], out var page);
        int.TryParse(query["pageSize"], out var pageSize);
        if (page < 1) page = 1;
        if (pageSize < 1 || pageSize > 100) pageSize = 50;

        try
        {
            var entryQuery = _db.AuditEntries
                .AsNoTracking()
                .Where(e => e.MspOrgId == parsedOrgId);

            // Tenant filter
            if (!string.IsNullOrEmpty(tenantIdFilter) && Guid.TryParse(tenantIdFilter, out var parsedTenantId))
            {
                entryQuery = entryQuery.Where(e => e.ClientTenantId == parsedTenantId);
            }

            // Action filter
            if (!string.IsNullOrEmpty(actionFilter))
            {
                entryQuery = entryQuery.Where(e => e.Action == actionFilter);
            }

            // Actor filter (partial match)
            if (!string.IsNullOrEmpty(actorFilter))
            {
                entryQuery = entryQuery.Where(e =>
                    e.ActorName.Contains(actorFilter) || e.ActorId.Contains(actorFilter));
            }

            // Date range
            if (!string.IsNullOrEmpty(fromDate) && DateTimeOffset.TryParse(fromDate, out var parsedFrom))
            {
                entryQuery = entryQuery.Where(e => e.Timestamp >= parsedFrom);
            }
            if (!string.IsNullOrEmpty(toDate) && DateTimeOffset.TryParse(toDate, out var parsedTo))
            {
                entryQuery = entryQuery.Where(e => e.Timestamp <= parsedTo);
            }

            var totalCount = await entryQuery.CountAsync(req.HttpContext.RequestAborted);

            // Get tenant name map for display
            var tenantNameMap = await _db.ClientTenants
                .AsNoTracking()
                .Where(t => t.MspOrgId == parsedOrgId)
                .ToDictionaryAsync(t => t.Id, t => t.DisplayName, req.HttpContext.RequestAborted);

            var entries = await entryQuery
                .OrderByDescending(e => e.Timestamp)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .Select(e => new AuditEntryDto
                {
                    Id = e.Id,
                    MspOrgId = e.MspOrgId,
                    ClientTenantId = e.ClientTenantId,
                    ActorId = e.ActorId,
                    ActorName = e.ActorName,
                    Action = e.Action,
                    Details = e.Details,
                    CorrelationId = e.CorrelationId,
                    Timestamp = e.Timestamp,
                })
                .ToListAsync(req.HttpContext.RequestAborted);

            // Set tenant names
            foreach (var entry in entries)
            {
                if (entry.ClientTenantId.HasValue &&
                    tenantNameMap.TryGetValue(entry.ClientTenantId.Value, out var name))
                {
                    entry.TenantName = name;
                }
            }

            return ResponseEnvelopeHelper.Ok(new AuditSearchResult
            {
                Entries = entries,
                Page = page,
                PageSize = pageSize,
                TotalCount = totalCount,
                TotalPages = (int)Math.Ceiling(totalCount / (double)pageSize),
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get audit entries for org {MspOrgId}", parsedOrgId);
            return ResponseEnvelopeHelper.Error(
                System.Net.HttpStatusCode.InternalServerError,
                "INTERNAL_ERROR",
                "Failed to get audit entries.",
                context.InvocationId);
        }
    }

    [Function("ExportAuditEntries")]
    public async Task<IActionResult> ExportAuditEntries(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "v1/audit/export")] HttpRequest req,
        FunctionContext context)
    {
        var mspOrgId = context.GetMspOrgId();
        if (string.IsNullOrEmpty(mspOrgId))
            return ResponseEnvelopeHelper.Unauthorized(context.InvocationId);

        if (!Guid.TryParse(mspOrgId, out var parsedOrgId))
            return ResponseEnvelopeHelper.BadRequest("Invalid organization ID.", context.InvocationId);

        var query = req.Query;
        var tenantIdFilter = query["tenantId"].FirstOrDefault();
        var actionFilter = query["action"].FirstOrDefault();
        var actorFilter = query["actor"].FirstOrDefault();
        var fromDate = query["from"].FirstOrDefault();
        var toDate = query["to"].FirstOrDefault();

        try
        {
            var entryQuery = _db.AuditEntries
                .AsNoTracking()
                .Where(e => e.MspOrgId == parsedOrgId);

            if (!string.IsNullOrEmpty(tenantIdFilter) && Guid.TryParse(tenantIdFilter, out var parsedTenantId))
                entryQuery = entryQuery.Where(e => e.ClientTenantId == parsedTenantId);
            if (!string.IsNullOrEmpty(actionFilter))
                entryQuery = entryQuery.Where(e => e.Action == actionFilter);
            if (!string.IsNullOrEmpty(actorFilter))
                entryQuery = entryQuery.Where(e => e.ActorName.Contains(actorFilter) || e.ActorId.Contains(actorFilter));
            if (!string.IsNullOrEmpty(fromDate) && DateTimeOffset.TryParse(fromDate, out var parsedFrom))
                entryQuery = entryQuery.Where(e => e.Timestamp >= parsedFrom);
            if (!string.IsNullOrEmpty(toDate) && DateTimeOffset.TryParse(toDate, out var parsedTo))
                entryQuery = entryQuery.Where(e => e.Timestamp <= parsedTo);

            var tenantNameMap = await _db.ClientTenants
                .AsNoTracking()
                .Where(t => t.MspOrgId == parsedOrgId)
                .ToDictionaryAsync(t => t.Id, t => t.DisplayName, req.HttpContext.RequestAborted);

            var entries = await entryQuery
                .OrderByDescending(e => e.Timestamp)
                .Take(10000)
                .Select(e => new AuditEntryDto
                {
                    Id = e.Id,
                    MspOrgId = e.MspOrgId,
                    ClientTenantId = e.ClientTenantId,
                    ActorId = e.ActorId,
                    ActorName = e.ActorName,
                    Action = e.Action,
                    Details = e.Details,
                    CorrelationId = e.CorrelationId,
                    Timestamp = e.Timestamp,
                })
                .ToListAsync(req.HttpContext.RequestAborted);

            foreach (var entry in entries)
            {
                if (entry.ClientTenantId.HasValue &&
                    tenantNameMap.TryGetValue(entry.ClientTenantId.Value, out var name))
                {
                    entry.TenantName = name;
                }
            }

            var sb = new StringBuilder();
            sb.AppendLine("Timestamp,Tenant,Actor,Action,Details,CorrelationId");
            foreach (var e in entries)
            {
                sb.Append(e.Timestamp.ToString("o")).Append(',');
                sb.Append(CsvEscape(e.TenantName ?? "")).Append(',');
                sb.Append(CsvEscape(e.ActorName)).Append(',');
                sb.Append(CsvEscape(e.Action)).Append(',');
                sb.Append(CsvEscape(e.Details ?? "")).Append(',');
                sb.AppendLine(CsvEscape(e.CorrelationId ?? ""));
            }

            var csvBytes = Encoding.UTF8.GetBytes(sb.ToString());
            var bom = Encoding.UTF8.GetPreamble();
            var result = new byte[bom.Length + csvBytes.Length];
            bom.CopyTo(result, 0);
            csvBytes.CopyTo(result, bom.Length);

            return new FileContentResult(result, "text/csv")
            {
                FileDownloadName = $"audit-export-{DateTimeOffset.UtcNow:yyyyMMdd-HHmmss}.csv",
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to export audit entries for org {MspOrgId}", parsedOrgId);
            return ResponseEnvelopeHelper.Error(
                System.Net.HttpStatusCode.InternalServerError,
                "INTERNAL_ERROR",
                "Failed to export audit entries.",
                context.InvocationId);
        }
    }

    private static string CsvEscape(string value)
    {
        if (value.Contains('"') || value.Contains(',') || value.Contains('\n') || value.Contains('\r'))
            return $"\"{value.Replace("\"", "\"\"")}\"";
        return value;
    }
}
