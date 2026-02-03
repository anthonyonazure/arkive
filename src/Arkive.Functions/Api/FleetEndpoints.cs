using Arkive.Core.Interfaces;
using Arkive.Functions.Extensions;
using Arkive.Functions.Middleware;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;

namespace Arkive.Functions.Api;

public class FleetEndpoints
{
    private readonly IFleetAnalyticsService _analyticsService;
    private readonly ILogger<FleetEndpoints> _logger;

    public FleetEndpoints(IFleetAnalyticsService analyticsService, ILogger<FleetEndpoints> logger)
    {
        _analyticsService = analyticsService;
        _logger = logger;
    }

    [Function("FleetOverview")]
    public async Task<IActionResult> FleetOverview(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "v1/fleet/overview")] HttpRequest req,
        FunctionContext context)
    {
        var mspOrgId = context.GetMspOrgId();
        if (string.IsNullOrEmpty(mspOrgId))
            return ResponseEnvelopeHelper.Unauthorized(context.InvocationId);

        if (!Guid.TryParse(mspOrgId, out var parsedOrgId))
            return ResponseEnvelopeHelper.BadRequest("Invalid organization ID.", context.InvocationId);

        try
        {
            var result = await _analyticsService.GetFleetOverviewAsync(parsedOrgId, req.HttpContext.RequestAborted);
            return ResponseEnvelopeHelper.Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to retrieve fleet overview for org {MspOrgId}", parsedOrgId);
            return ResponseEnvelopeHelper.Error(System.Net.HttpStatusCode.InternalServerError, "INTERNAL_ERROR", "Failed to retrieve fleet overview.", context.InvocationId);
        }
    }

    [Function("TenantAnalytics")]
    public async Task<IActionResult> TenantAnalytics(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "v1/tenants/{tenantId}/analytics")] HttpRequest req,
        string tenantId,
        FunctionContext context)
    {
        var mspOrgId = context.GetMspOrgId();
        if (string.IsNullOrEmpty(mspOrgId))
            return ResponseEnvelopeHelper.Unauthorized(context.InvocationId);

        if (!Guid.TryParse(mspOrgId, out var parsedOrgId))
            return ResponseEnvelopeHelper.BadRequest("Invalid organization ID.", context.InvocationId);

        if (!Guid.TryParse(tenantId, out var parsedTenantId))
            return ResponseEnvelopeHelper.BadRequest("Invalid tenant ID.", context.InvocationId);

        try
        {
            var result = await _analyticsService.GetTenantAnalyticsAsync(parsedTenantId, parsedOrgId, req.HttpContext.RequestAborted);
            return ResponseEnvelopeHelper.Ok(result);
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogWarning(ex, "Tenant {TenantId} not found for org {MspOrgId}", parsedTenantId, parsedOrgId);
            return ResponseEnvelopeHelper.NotFound("Tenant not found.", context.InvocationId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to retrieve tenant analytics for {TenantId}", parsedTenantId);
            return ResponseEnvelopeHelper.Error(System.Net.HttpStatusCode.InternalServerError, "INTERNAL_ERROR", "Failed to retrieve tenant analytics.", context.InvocationId);
        }
    }

    [Function("SiteFiles")]
    public async Task<IActionResult> SiteFiles(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "v1/tenants/{tenantId}/sites/{siteId}/files")] HttpRequest req,
        string tenantId,
        string siteId,
        FunctionContext context)
    {
        var mspOrgId = context.GetMspOrgId();
        if (string.IsNullOrEmpty(mspOrgId))
            return ResponseEnvelopeHelper.Unauthorized(context.InvocationId);

        if (!Guid.TryParse(mspOrgId, out var parsedOrgId))
            return ResponseEnvelopeHelper.BadRequest("Invalid organization ID.", context.InvocationId);

        if (!Guid.TryParse(tenantId, out var parsedTenantId))
            return ResponseEnvelopeHelper.BadRequest("Invalid tenant ID.", context.InvocationId);

        // Parse query parameters
        var query = req.Query;
        int.TryParse(query["page"], out var page);
        int.TryParse(query["pageSize"], out var pageSize);
        if (page < 1) page = 1;
        if (pageSize < 1 || pageSize > 200) pageSize = 50;

        var sortBy = query["sortBy"].FirstOrDefault();
        var sortDir = query["sortDir"].FirstOrDefault();

        int? minAgeDays = int.TryParse(query["minAgeDays"], out var mad) ? mad : null;
        var fileType = query["fileType"].FirstOrDefault();
        long? minSizeBytes = long.TryParse(query["minSizeBytes"], out var minS) ? minS : null;
        long? maxSizeBytes = long.TryParse(query["maxSizeBytes"], out var maxS) ? maxS : null;

        try
        {
            var result = await _analyticsService.GetSiteFilesAsync(
                parsedTenantId, siteId, parsedOrgId,
                page, pageSize, sortBy, sortDir,
                minAgeDays, fileType, minSizeBytes, maxSizeBytes,
                req.HttpContext.RequestAborted);
            return ResponseEnvelopeHelper.Ok(result);
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogWarning(ex, "Site {SiteId} not found for tenant {TenantId}", siteId, parsedTenantId);
            return ResponseEnvelopeHelper.NotFound("Site not found.", context.InvocationId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to retrieve site files for {SiteId} in tenant {TenantId}", siteId, parsedTenantId);
            return ResponseEnvelopeHelper.Error(System.Net.HttpStatusCode.InternalServerError, "INTERNAL_ERROR", "Failed to retrieve site files.", context.InvocationId);
        }
    }
}
