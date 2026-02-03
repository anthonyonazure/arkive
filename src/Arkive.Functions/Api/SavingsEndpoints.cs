using Arkive.Core.Interfaces;
using Arkive.Functions.Extensions;
using Arkive.Functions.Middleware;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;

namespace Arkive.Functions.Api;

public class SavingsEndpoints
{
    private readonly ISavingsSnapshotService _snapshotService;
    private readonly ILogger<SavingsEndpoints> _logger;

    public SavingsEndpoints(ISavingsSnapshotService snapshotService, ILogger<SavingsEndpoints> logger)
    {
        _snapshotService = snapshotService;
        _logger = logger;
    }

    [Function("GetSavingsTrends")]
    public async Task<IActionResult> GetSavingsTrends(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "v1/savings/trends")] HttpRequest req,
        FunctionContext context)
    {
        var mspOrgId = context.GetMspOrgId();
        if (string.IsNullOrEmpty(mspOrgId))
            return ResponseEnvelopeHelper.Unauthorized(context.InvocationId);

        if (!Guid.TryParse(mspOrgId, out var parsedOrgId))
            return ResponseEnvelopeHelper.BadRequest("Invalid organization ID.", context.InvocationId);

        var query = req.Query;
        var tenantIdParam = query["tenantId"].FirstOrDefault();
        Guid? tenantId = null;
        if (!string.IsNullOrEmpty(tenantIdParam) && Guid.TryParse(tenantIdParam, out var parsedTenantId))
            tenantId = parsedTenantId;

        int.TryParse(query["months"], out var months);
        if (months < 1 || months > 24) months = 12;

        try
        {
            var result = await _snapshotService.GetTrendsAsync(parsedOrgId, tenantId, months, req.HttpContext.RequestAborted);
            return ResponseEnvelopeHelper.Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to retrieve savings trends for org {MspOrgId}", parsedOrgId);
            return ResponseEnvelopeHelper.Error(
                System.Net.HttpStatusCode.InternalServerError,
                "INTERNAL_ERROR",
                "Failed to retrieve savings trends.",
                context.InvocationId);
        }
    }
}
