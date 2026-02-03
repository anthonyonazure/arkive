using Arkive.Core.DTOs;
using Arkive.Core.Interfaces;
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

public class ArchiveEndpoints
{
    private readonly ArkiveDbContext _db;
    private readonly ILogger<ArchiveEndpoints> _logger;

    public ArchiveEndpoints(ArkiveDbContext db, ILogger<ArchiveEndpoints> logger)
    {
        _db = db;
        _logger = logger;
    }

    [Function("TriggerArchive")]
    public async Task<IActionResult> TriggerArchive(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "v1/tenants/{tenantId}/archive")] HttpRequest req,
        string tenantId,
        [DurableClient] DurableTaskClient durableClient,
        FunctionContext context)
    {
        var mspOrgId = context.GetMspOrgId();
        if (string.IsNullOrEmpty(mspOrgId))
            return ResponseEnvelopeHelper.Unauthorized(context.InvocationId);

        if (!Guid.TryParse(mspOrgId, out var parsedOrgId))
            return ResponseEnvelopeHelper.BadRequest("Invalid organization ID.", context.InvocationId);

        if (!Guid.TryParse(tenantId, out var parsedTenantId))
            return ResponseEnvelopeHelper.BadRequest("Invalid tenant ID.", context.InvocationId);

        TriggerArchiveRequest? request;
        try
        {
            request = await req.ReadFromJsonAsync<TriggerArchiveRequest>(req.HttpContext.RequestAborted);
        }
        catch
        {
            request = new TriggerArchiveRequest();
        }

        request ??= new TriggerArchiveRequest();

        try
        {
            // Generate deterministic orchestration ID to prevent duplicate runs
            var instanceId = $"archive-{parsedTenantId}-{request.RuleId ?? Guid.Empty}";

            // Check if an archive orchestration is already running
            var existingInstance = await durableClient.GetInstanceAsync(instanceId, req.HttpContext.RequestAborted);
            if (existingInstance is not null &&
                existingInstance.RuntimeStatus is OrchestrationRuntimeStatus.Running or OrchestrationRuntimeStatus.Pending)
            {
                return ResponseEnvelopeHelper.BadRequest(
                    "An archive operation is already in progress for this tenant.", context.InvocationId);
            }

            // Look up tenant M365 ID
            var m365TenantId = await GetM365TenantIdAsync(parsedTenantId, parsedOrgId, req.HttpContext.RequestAborted);
            if (m365TenantId is null)
                return ResponseEnvelopeHelper.NotFound("Tenant not found.", context.InvocationId);

            var input = new ArchiveOrchestrationInput
            {
                TenantId = parsedTenantId,
                MspOrgId = parsedOrgId,
                M365TenantId = m365TenantId,
                RuleId = request.RuleId,
            };

            await durableClient.ScheduleNewOrchestrationInstanceAsync(
                nameof(ArchiveOrchestrator),
                input,
                new StartOrchestrationOptions { InstanceId = instanceId },
                req.HttpContext.RequestAborted);

            _logger.LogInformation(
                "Started archive orchestration {InstanceId} for tenant {TenantId}",
                instanceId, parsedTenantId);

            return ResponseEnvelopeHelper.Ok(new { orchestrationId = instanceId, status = "Started" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to trigger archive for tenant {TenantId}", parsedTenantId);
            return ResponseEnvelopeHelper.Error(
                System.Net.HttpStatusCode.InternalServerError,
                "INTERNAL_ERROR",
                "Failed to trigger archive operation.",
                context.InvocationId);
        }
    }

    [Function("GetArchiveStatus")]
    public async Task<IActionResult> GetArchiveStatus(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "v1/tenants/{tenantId}/archive/status")] HttpRequest req,
        string tenantId,
        [DurableClient] DurableTaskClient durableClient,
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
            // Support optional ruleId query param to check rule-specific orchestrations
            Guid ruleId = Guid.Empty;
            if (req.Query.ContainsKey("ruleId") && Guid.TryParse(req.Query["ruleId"], out var parsedRuleId))
                ruleId = parsedRuleId;

            var instanceId = $"archive-{parsedTenantId}-{ruleId}";
            var instance = await durableClient.GetInstanceAsync(instanceId, req.HttpContext.RequestAborted);

            if (instance is null)
                return ResponseEnvelopeHelper.NotFound("No archive operation found for this tenant.", context.InvocationId);

            var status = new ArchiveStatusDto
            {
                OrchestrationId = instanceId,
                Status = instance.RuntimeStatus.ToString(),
                StartedAt = instance.CreatedAt,
                CompletedAt = instance.LastUpdatedAt,
            };

            // Try to parse the output for detailed counts
            if (instance.RuntimeStatus is OrchestrationRuntimeStatus.Completed && instance.SerializedOutput is not null)
            {
                try
                {
                    var output = System.Text.Json.JsonSerializer.Deserialize<ArchiveOrchestrationResult>(
                        instance.SerializedOutput);
                    if (output is not null)
                    {
                        status.TotalFiles = output.TotalFiles;
                        status.CompletedFiles = output.CompletedFiles;
                        status.FailedFiles = output.FailedFiles;
                    }
                }
                catch
                {
                    // Ignore deserialization errors
                }
            }

            return ResponseEnvelopeHelper.Ok(status);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get archive status for tenant {TenantId}", parsedTenantId);
            return ResponseEnvelopeHelper.Error(
                System.Net.HttpStatusCode.InternalServerError,
                "INTERNAL_ERROR",
                "Failed to get archive status.",
                context.InvocationId);
        }
    }

    private async Task<string?> GetM365TenantIdAsync(Guid tenantId, Guid mspOrgId, CancellationToken cancellationToken)
    {
        var tenant = await _db.ClientTenants
            .AsNoTracking()
            .Where(t => t.Id == tenantId && t.MspOrgId == mspOrgId)
            .Select(t => t.M365TenantId)
            .FirstOrDefaultAsync(cancellationToken);

        return tenant;
    }
}
