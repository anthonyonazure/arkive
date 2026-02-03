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

public class ScanEndpoints
{
    private readonly ArkiveDbContext _db;
    private readonly ILogger<ScanEndpoints> _logger;

    public ScanEndpoints(ArkiveDbContext db, ILogger<ScanEndpoints> logger)
    {
        _db = db;
        _logger = logger;
    }

    [Function("StartTenantScan")]
    public async Task<IActionResult> StartTenantScan(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "v1/tenants/{id}/scan")] HttpRequest req,
        [DurableClient] DurableTaskClient durableClient,
        FunctionContext context,
        string id)
    {
        var mspOrgId = context.GetMspOrgId();
        if (string.IsNullOrEmpty(mspOrgId))
            return ResponseEnvelopeHelper.Unauthorized(context.InvocationId);

        if (!Guid.TryParse(id, out var tenantId))
            return ResponseEnvelopeHelper.BadRequest("Invalid tenant ID.", context.InvocationId);

        if (!Guid.TryParse(mspOrgId, out var parsedOrgId))
            return ResponseEnvelopeHelper.BadRequest("Invalid organization ID.", context.InvocationId);

        // Verify tenant exists and is connected
        var tenant = await _db.ClientTenants
            .AsNoTracking()
            .FirstOrDefaultAsync(t => t.Id == tenantId, req.HttpContext.RequestAborted);

        if (tenant is null)
            return ResponseEnvelopeHelper.NotFound($"Tenant {tenantId} not found.", context.InvocationId);

        if (tenant.Status != Arkive.Core.Enums.TenantStatus.Connected)
            return ResponseEnvelopeHelper.BadRequest("Tenant must be in Connected status to start a scan.", context.InvocationId);

        // Prevent duplicate scans — use deterministic instance ID per tenant
        var instanceId = $"scan-{tenantId}";
        var existingInstance = await durableClient.GetInstanceAsync(instanceId, cancellation: req.HttpContext.RequestAborted);
        if (existingInstance is not null &&
            existingInstance.RuntimeStatus is OrchestrationRuntimeStatus.Running or OrchestrationRuntimeStatus.Pending)
        {
            _logger.LogWarning("Scan already running for tenant {TenantId}, instance {InstanceId}", tenantId, instanceId);
            return ResponseEnvelopeHelper.Conflict("A scan is already running for this tenant.", context.InvocationId);
        }

        var input = new ScanOrchestrationInput
        {
            TenantId = tenantId,
            MspOrgId = parsedOrgId,
            M365TenantId = tenant.M365TenantId
        };

        await durableClient.ScheduleNewOrchestrationInstanceAsync(
            nameof(TenantScanOrchestrator),
            input,
            new StartOrchestrationOptions { InstanceId = instanceId },
            cancellation: req.HttpContext.RequestAborted);

        _logger.LogInformation("Started scan orchestration {InstanceId} for tenant {TenantId}",
            instanceId, tenantId);

        return ResponseEnvelopeHelper.Ok(new { instanceId });
    }

    [Function("GetTenantScanStatus")]
    public async Task<IActionResult> GetTenantScanStatus(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "v1/tenants/{id}/scan/status")] HttpRequest req,
        [DurableClient] DurableTaskClient durableClient,
        FunctionContext context,
        string id)
    {
        var mspOrgId = context.GetMspOrgId();
        if (string.IsNullOrEmpty(mspOrgId))
            return ResponseEnvelopeHelper.Unauthorized(context.InvocationId);

        if (!Guid.TryParse(id, out var tenantId))
            return ResponseEnvelopeHelper.BadRequest("Invalid tenant ID.", context.InvocationId);

        // Use deterministic instance ID to look up scan for this tenant
        var instanceId = $"scan-{tenantId}";
        var metadata = await durableClient.GetInstanceAsync(instanceId, cancellation: req.HttpContext.RequestAborted);

        if (metadata is null)
            return ResponseEnvelopeHelper.NotFound($"No scan found for tenant {tenantId}.", context.InvocationId);

        return ResponseEnvelopeHelper.Ok(new
        {
            instanceId = metadata.InstanceId,
            runtimeStatus = metadata.RuntimeStatus.ToString(),
            createdAt = metadata.CreatedAt,
            lastUpdatedAt = metadata.LastUpdatedAt
        });
    }
}
