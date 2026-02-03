using Arkive.Core.DTOs;
using Arkive.Core.Interfaces;
using Arkive.Functions.Extensions;
using Arkive.Functions.Middleware;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;

namespace Arkive.Functions.Api;

public class ArchiveRuleEndpoints
{
    private readonly IArchiveRuleService _ruleService;
    private readonly IRuleEvaluationService _evaluationService;
    private readonly IAuditService _auditService;
    private readonly ILogger<ArchiveRuleEndpoints> _logger;

    public ArchiveRuleEndpoints(
        IArchiveRuleService ruleService,
        IRuleEvaluationService evaluationService,
        IAuditService auditService,
        ILogger<ArchiveRuleEndpoints> logger)
    {
        _ruleService = ruleService;
        _evaluationService = evaluationService;
        _auditService = auditService;
        _logger = logger;
    }

    [Function("CreateArchiveRule")]
    public async Task<IActionResult> CreateRule(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "v1/tenants/{tenantId}/rules")] HttpRequest req,
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

        CreateArchiveRuleRequest? request;
        try
        {
            request = await req.ReadFromJsonAsync<CreateArchiveRuleRequest>(req.HttpContext.RequestAborted);
        }
        catch
        {
            return ResponseEnvelopeHelper.BadRequest("Invalid request body.", context.InvocationId);
        }

        if (request is null)
            return ResponseEnvelopeHelper.BadRequest("Request body is required.", context.InvocationId);

        try
        {
            var result = await _ruleService.CreateAsync(
                parsedTenantId, parsedOrgId, request, null, req.HttpContext.RequestAborted);

            await _auditService.LogAsync(new AuditInput
            {
                MspOrgId = parsedOrgId,
                ClientTenantId = parsedTenantId,
                Action = "RuleCreated",
                Details = new { ruleId = result.Id, ruleName = result.Name, ruleType = result.RuleType },
            }, req.HttpContext.RequestAborted);

            var location = $"/v1/tenants/{tenantId}/rules/{result.Id}";
            return ResponseEnvelopeHelper.Created(location, result);
        }
        catch (ArgumentException ex)
        {
            return ResponseEnvelopeHelper.BadRequest(ex.Message, context.InvocationId);
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogWarning(ex, "Tenant {TenantId} not found for org {MspOrgId}", parsedTenantId, parsedOrgId);
            return ResponseEnvelopeHelper.NotFound("Tenant not found.", context.InvocationId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to create archive rule for tenant {TenantId}", parsedTenantId);
            return ResponseEnvelopeHelper.Error(
                System.Net.HttpStatusCode.InternalServerError,
                "INTERNAL_ERROR",
                "Failed to create archive rule.",
                context.InvocationId);
        }
    }

    [Function("ListArchiveRules")]
    public async Task<IActionResult> ListRules(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "v1/tenants/{tenantId}/rules")] HttpRequest req,
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

        var ruleType = req.Query["ruleType"].FirstOrDefault();

        try
        {
            var result = await _ruleService.GetAllByTenantAsync(
                parsedTenantId, parsedOrgId, ruleType, req.HttpContext.RequestAborted);
            return ResponseEnvelopeHelper.Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to list archive rules for tenant {TenantId}", parsedTenantId);
            return ResponseEnvelopeHelper.Error(
                System.Net.HttpStatusCode.InternalServerError,
                "INTERNAL_ERROR",
                "Failed to list archive rules.",
                context.InvocationId);
        }
    }

    [Function("GetArchiveRule")]
    public async Task<IActionResult> GetRule(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "v1/tenants/{tenantId}/rules/{ruleId}")] HttpRequest req,
        string tenantId,
        string ruleId,
        FunctionContext context)
    {
        var mspOrgId = context.GetMspOrgId();
        if (string.IsNullOrEmpty(mspOrgId))
            return ResponseEnvelopeHelper.Unauthorized(context.InvocationId);

        if (!Guid.TryParse(mspOrgId, out var parsedOrgId))
            return ResponseEnvelopeHelper.BadRequest("Invalid organization ID.", context.InvocationId);

        if (!Guid.TryParse(tenantId, out var parsedTenantId))
            return ResponseEnvelopeHelper.BadRequest("Invalid tenant ID.", context.InvocationId);

        if (!Guid.TryParse(ruleId, out var parsedRuleId))
            return ResponseEnvelopeHelper.BadRequest("Invalid rule ID.", context.InvocationId);

        try
        {
            var result = await _ruleService.GetByIdAsync(
                parsedTenantId, parsedRuleId, parsedOrgId, req.HttpContext.RequestAborted);

            if (result is null)
                return ResponseEnvelopeHelper.NotFound("Rule not found.", context.InvocationId);

            return ResponseEnvelopeHelper.Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get archive rule {RuleId} for tenant {TenantId}", ruleId, parsedTenantId);
            return ResponseEnvelopeHelper.Error(
                System.Net.HttpStatusCode.InternalServerError,
                "INTERNAL_ERROR",
                "Failed to get archive rule.",
                context.InvocationId);
        }
    }

    [Function("UpdateArchiveRule")]
    public async Task<IActionResult> UpdateRule(
        [HttpTrigger(AuthorizationLevel.Anonymous, "put", Route = "v1/tenants/{tenantId}/rules/{ruleId}")] HttpRequest req,
        string tenantId,
        string ruleId,
        FunctionContext context)
    {
        var mspOrgId = context.GetMspOrgId();
        if (string.IsNullOrEmpty(mspOrgId))
            return ResponseEnvelopeHelper.Unauthorized(context.InvocationId);

        if (!Guid.TryParse(mspOrgId, out var parsedOrgId))
            return ResponseEnvelopeHelper.BadRequest("Invalid organization ID.", context.InvocationId);

        if (!Guid.TryParse(tenantId, out var parsedTenantId))
            return ResponseEnvelopeHelper.BadRequest("Invalid tenant ID.", context.InvocationId);

        if (!Guid.TryParse(ruleId, out var parsedRuleId))
            return ResponseEnvelopeHelper.BadRequest("Invalid rule ID.", context.InvocationId);

        UpdateArchiveRuleRequest? request;
        try
        {
            request = await req.ReadFromJsonAsync<UpdateArchiveRuleRequest>(req.HttpContext.RequestAborted);
        }
        catch
        {
            return ResponseEnvelopeHelper.BadRequest("Invalid request body.", context.InvocationId);
        }

        if (request is null)
            return ResponseEnvelopeHelper.BadRequest("Request body is required.", context.InvocationId);

        try
        {
            var result = await _ruleService.UpdateAsync(
                parsedTenantId, parsedRuleId, parsedOrgId, request, req.HttpContext.RequestAborted);

            if (result is null)
                return ResponseEnvelopeHelper.NotFound("Rule not found.", context.InvocationId);

            await _auditService.LogAsync(new AuditInput
            {
                MspOrgId = parsedOrgId,
                ClientTenantId = parsedTenantId,
                Action = "RuleUpdated",
                Details = new { ruleId = result.Id, ruleName = result.Name, ruleType = result.RuleType },
            }, req.HttpContext.RequestAborted);

            return ResponseEnvelopeHelper.Ok(result);
        }
        catch (ArgumentException ex)
        {
            return ResponseEnvelopeHelper.BadRequest(ex.Message, context.InvocationId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to update archive rule {RuleId} for tenant {TenantId}", ruleId, parsedTenantId);
            return ResponseEnvelopeHelper.Error(
                System.Net.HttpStatusCode.InternalServerError,
                "INTERNAL_ERROR",
                "Failed to update archive rule.",
                context.InvocationId);
        }
    }

    [Function("DeleteArchiveRule")]
    public async Task<IActionResult> DeleteRule(
        [HttpTrigger(AuthorizationLevel.Anonymous, "delete", Route = "v1/tenants/{tenantId}/rules/{ruleId}")] HttpRequest req,
        string tenantId,
        string ruleId,
        FunctionContext context)
    {
        var mspOrgId = context.GetMspOrgId();
        if (string.IsNullOrEmpty(mspOrgId))
            return ResponseEnvelopeHelper.Unauthorized(context.InvocationId);

        if (!Guid.TryParse(mspOrgId, out var parsedOrgId))
            return ResponseEnvelopeHelper.BadRequest("Invalid organization ID.", context.InvocationId);

        if (!Guid.TryParse(tenantId, out var parsedTenantId))
            return ResponseEnvelopeHelper.BadRequest("Invalid tenant ID.", context.InvocationId);

        if (!Guid.TryParse(ruleId, out var parsedRuleId))
            return ResponseEnvelopeHelper.BadRequest("Invalid rule ID.", context.InvocationId);

        try
        {
            var deleted = await _ruleService.DeleteAsync(
                parsedTenantId, parsedRuleId, parsedOrgId, req.HttpContext.RequestAborted);

            if (!deleted)
                return ResponseEnvelopeHelper.NotFound("Rule not found.", context.InvocationId);

            await _auditService.LogAsync(new AuditInput
            {
                MspOrgId = parsedOrgId,
                ClientTenantId = parsedTenantId,
                Action = "RuleDeleted",
                Details = new { ruleId = parsedRuleId },
            }, req.HttpContext.RequestAborted);

            return ResponseEnvelopeHelper.Ok(new { deleted = true });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to delete archive rule {RuleId} for tenant {TenantId}", ruleId, parsedTenantId);
            return ResponseEnvelopeHelper.Error(
                System.Net.HttpStatusCode.InternalServerError,
                "INTERNAL_ERROR",
                "Failed to delete archive rule.",
                context.InvocationId);
        }
    }

    [Function("PreviewArchiveRule")]
    public async Task<IActionResult> PreviewRule(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "v1/tenants/{tenantId}/rules/{ruleId}/preview")] HttpRequest req,
        string tenantId,
        string ruleId,
        FunctionContext context)
    {
        var mspOrgId = context.GetMspOrgId();
        if (string.IsNullOrEmpty(mspOrgId))
            return ResponseEnvelopeHelper.Unauthorized(context.InvocationId);

        if (!Guid.TryParse(mspOrgId, out var parsedOrgId))
            return ResponseEnvelopeHelper.BadRequest("Invalid organization ID.", context.InvocationId);

        if (!Guid.TryParse(tenantId, out var parsedTenantId))
            return ResponseEnvelopeHelper.BadRequest("Invalid tenant ID.", context.InvocationId);

        if (!Guid.TryParse(ruleId, out var parsedRuleId))
            return ResponseEnvelopeHelper.BadRequest("Invalid rule ID.", context.InvocationId);

        try
        {
            var result = await _evaluationService.PreviewRuleAsync(
                parsedTenantId, parsedRuleId, parsedOrgId, req.HttpContext.RequestAborted);
            return ResponseEnvelopeHelper.Ok(result);
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogWarning(ex, "Rule {RuleId} not found for preview", ruleId);
            return ResponseEnvelopeHelper.NotFound("Rule not found.", context.InvocationId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to preview rule {RuleId} for tenant {TenantId}", ruleId, parsedTenantId);
            return ResponseEnvelopeHelper.Error(
                System.Net.HttpStatusCode.InternalServerError,
                "INTERNAL_ERROR",
                "Failed to preview rule.",
                context.InvocationId);
        }
    }

    [Function("PreviewAdHocRule")]
    public async Task<IActionResult> PreviewAdHocRule(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "v1/tenants/{tenantId}/rules/preview")] HttpRequest req,
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

        DryRunPreviewRequest? request;
        try
        {
            request = await req.ReadFromJsonAsync<DryRunPreviewRequest>(req.HttpContext.RequestAborted);
        }
        catch
        {
            return ResponseEnvelopeHelper.BadRequest("Invalid request body.", context.InvocationId);
        }

        if (request is null)
            return ResponseEnvelopeHelper.BadRequest("Request body is required.", context.InvocationId);

        try
        {
            var result = await _evaluationService.PreviewAdHocRuleAsync(
                parsedTenantId, parsedOrgId, request, req.HttpContext.RequestAborted);
            return ResponseEnvelopeHelper.Ok(result);
        }
        catch (ArgumentException ex)
        {
            return ResponseEnvelopeHelper.BadRequest(ex.Message, context.InvocationId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to preview ad-hoc rule for tenant {TenantId}", parsedTenantId);
            return ResponseEnvelopeHelper.Error(
                System.Net.HttpStatusCode.InternalServerError,
                "INTERNAL_ERROR",
                "Failed to preview rule.",
                context.InvocationId);
        }
    }

    [Function("GetExclusionScope")]
    public async Task<IActionResult> GetExclusionScope(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "v1/tenants/{tenantId}/rules/{ruleId}/scope")] HttpRequest req,
        string tenantId,
        string ruleId,
        FunctionContext context)
    {
        var mspOrgId = context.GetMspOrgId();
        if (string.IsNullOrEmpty(mspOrgId))
            return ResponseEnvelopeHelper.Unauthorized(context.InvocationId);

        if (!Guid.TryParse(mspOrgId, out var parsedOrgId))
            return ResponseEnvelopeHelper.BadRequest("Invalid organization ID.", context.InvocationId);

        if (!Guid.TryParse(tenantId, out var parsedTenantId))
            return ResponseEnvelopeHelper.BadRequest("Invalid tenant ID.", context.InvocationId);

        if (!Guid.TryParse(ruleId, out var parsedRuleId))
            return ResponseEnvelopeHelper.BadRequest("Invalid rule ID.", context.InvocationId);

        try
        {
            var result = await _evaluationService.GetExclusionScopeAsync(
                parsedTenantId, parsedRuleId, parsedOrgId, req.HttpContext.RequestAborted);
            return ResponseEnvelopeHelper.Ok(result);
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogWarning(ex, "Exclusion rule {RuleId} not found for tenant {TenantId}", ruleId, parsedTenantId);
            return ResponseEnvelopeHelper.NotFound("Exclusion rule not found.", context.InvocationId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get exclusion scope for rule {RuleId}", ruleId);
            return ResponseEnvelopeHelper.Error(
                System.Net.HttpStatusCode.InternalServerError,
                "INTERNAL_ERROR",
                "Failed to get exclusion scope.",
                context.InvocationId);
        }
    }
}
