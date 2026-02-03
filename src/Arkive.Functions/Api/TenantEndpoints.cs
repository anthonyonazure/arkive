using Arkive.Core.DTOs;
using Arkive.Core.Interfaces;
using Arkive.Data;
using Arkive.Functions.Extensions;
using Arkive.Functions.Middleware;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Arkive.Functions.Api;

public class TenantEndpoints
{
    private readonly ITenantOnboardingService _service;
    private readonly ArkiveDbContext _db;
    private readonly IAuditService _auditService;
    private readonly ILogger<TenantEndpoints> _logger;

    public TenantEndpoints(ITenantOnboardingService service, ArkiveDbContext db, IAuditService auditService, ILogger<TenantEndpoints> logger)
    {
        _service = service;
        _db = db;
        _auditService = auditService;
        _logger = logger;
    }

    [Function("ValidateTenantDomain")]
    public async Task<IActionResult> ValidateTenantDomain(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "v1/tenants/validate-domain")] HttpRequest req,
        FunctionContext context)
    {
        var mspOrgId = context.GetMspOrgId();
        if (string.IsNullOrEmpty(mspOrgId))
            return ResponseEnvelopeHelper.Unauthorized(context.InvocationId);

        var body = await req.ReadFromJsonAsync<ValidateDomainRequest>();
        if (body is null || string.IsNullOrWhiteSpace(body.Domain))
            return ResponseEnvelopeHelper.BadRequest("Domain is required.", context.InvocationId);

        var result = await _service.ValidateTenantDomainAsync(body.Domain);
        return ResponseEnvelopeHelper.Ok(result);
    }

    [Function("CreateTenant")]
    public async Task<IActionResult> CreateTenant(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "v1/tenants")] HttpRequest req,
        FunctionContext context)
    {
        var mspOrgId = context.GetMspOrgId();
        if (string.IsNullOrEmpty(mspOrgId))
            return ResponseEnvelopeHelper.Unauthorized(context.InvocationId);

        var body = await req.ReadFromJsonAsync<CreateTenantRequest>();
        if (body is null)
            return ResponseEnvelopeHelper.BadRequest("Request body is required.", context.InvocationId);

        if (string.IsNullOrWhiteSpace(body.M365TenantId) || body.M365TenantId.Length > 36)
            return ResponseEnvelopeHelper.BadRequest("M365TenantId is required and must not exceed 36 characters.", context.InvocationId);

        if (string.IsNullOrWhiteSpace(body.DisplayName) || body.DisplayName.Length > 200)
            return ResponseEnvelopeHelper.BadRequest("DisplayName is required and must not exceed 200 characters.", context.InvocationId);

        try
        {
            var result = await _service.CreateTenantAsync(Guid.Parse(mspOrgId), body);

            await _auditService.LogAsync(new AuditInput
            {
                MspOrgId = Guid.Parse(mspOrgId),
                ClientTenantId = result.Id,
                Action = "TenantCreated",
                Details = new { tenantId = result.Id, displayName = result.DisplayName, m365TenantId = body.M365TenantId },
            }, req.HttpContext.RequestAborted);

            return ResponseEnvelopeHelper.Created($"/api/v1/tenants/{result.Id}", result);
        }
        catch (DbUpdateException ex)
        {
            _logger.LogWarning(ex, "Database constraint violation creating tenant for M365TenantId {M365TenantId}", body.M365TenantId);
            return ResponseEnvelopeHelper.Conflict("A tenant with this M365 tenant ID already exists for your organization.", context.InvocationId);
        }
    }

    [Function("ConsentCallback")]
    public async Task<IActionResult> ConsentCallback(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "v1/tenants/{id}/consent-callback")] HttpRequest req,
        FunctionContext context,
        string id)
    {
        var mspOrgId = context.GetMspOrgId();
        if (string.IsNullOrEmpty(mspOrgId))
            return ResponseEnvelopeHelper.Unauthorized(context.InvocationId);

        if (!Guid.TryParse(id, out var tenantId))
            return ResponseEnvelopeHelper.BadRequest("Invalid tenant ID.", context.InvocationId);

        var body = await req.ReadFromJsonAsync<ConsentCallbackRequest>();
        if (body is null)
            return ResponseEnvelopeHelper.BadRequest("Request body is required.", context.InvocationId);

        if (string.IsNullOrWhiteSpace(body.M365TenantId) || body.M365TenantId.Length > 36)
            return ResponseEnvelopeHelper.BadRequest("M365TenantId is required and must not exceed 36 characters.", context.InvocationId);

        try
        {
            var result = await _service.ProcessConsentCallbackAsync(tenantId, body);
            return ResponseEnvelopeHelper.Ok(result);
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogWarning(ex, "Tenant not found for consent callback: {TenantId}", tenantId);
            return ResponseEnvelopeHelper.NotFound($"Tenant {tenantId} not found.", context.InvocationId);
        }
    }

    [Function("ListTenants")]
    public async Task<IActionResult> ListTenants(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "v1/tenants")] HttpRequest req,
        FunctionContext context)
    {
        var mspOrgId = context.GetMspOrgId();
        if (string.IsNullOrEmpty(mspOrgId))
            return ResponseEnvelopeHelper.Unauthorized(context.InvocationId);

        var result = await _service.GetTenantsAsync(Guid.Parse(mspOrgId));
        return ResponseEnvelopeHelper.Ok(result);
    }

    [Function("DisconnectTenant")]
    public async Task<IActionResult> DisconnectTenant(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "v1/tenants/{id}/disconnect")] HttpRequest req,
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

        try
        {
            var result = await _service.DisconnectTenantAsync(tenantId, parsedOrgId, req.HttpContext.RequestAborted);

            await _auditService.LogAsync(new AuditInput
            {
                MspOrgId = parsedOrgId,
                ClientTenantId = tenantId,
                Action = "TenantDisconnected",
                Details = new { tenantId },
            }, req.HttpContext.RequestAborted);

            return ResponseEnvelopeHelper.Ok(result);
        }
        catch (InvalidOperationException ex) when (ex.Message.Contains("not found", StringComparison.OrdinalIgnoreCase))
        {
            _logger.LogWarning(ex, "Tenant not found for disconnect: {TenantId}", tenantId);
            return ResponseEnvelopeHelper.NotFound(ex.Message, context.InvocationId);
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogWarning(ex, "Invalid tenant state for disconnect: {TenantId}", tenantId);
            return ResponseEnvelopeHelper.BadRequest(ex.Message, context.InvocationId);
        }
    }

    [Function("DiscoverSharePointSites")]
    public async Task<IActionResult> DiscoverSharePointSites(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "v1/tenants/{id}/sites")] HttpRequest req,
        FunctionContext context,
        string id)
    {
        var mspOrgId = context.GetMspOrgId();
        if (string.IsNullOrEmpty(mspOrgId))
            return ResponseEnvelopeHelper.Unauthorized(context.InvocationId);

        if (!Guid.TryParse(id, out var tenantId))
            return ResponseEnvelopeHelper.BadRequest("Invalid tenant ID.", context.InvocationId);

        try
        {
            var result = await _service.DiscoverSharePointSitesAsync(tenantId);
            return ResponseEnvelopeHelper.Ok(result);
        }
        catch (InvalidOperationException ex) when (ex.Message.Contains("not found", StringComparison.OrdinalIgnoreCase))
        {
            _logger.LogWarning(ex, "Tenant not found for site discovery: {TenantId}", tenantId);
            return ResponseEnvelopeHelper.NotFound(ex.Message, context.InvocationId);
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogWarning(ex, "Invalid tenant state for site discovery: {TenantId}", tenantId);
            return ResponseEnvelopeHelper.BadRequest(ex.Message, context.InvocationId);
        }
    }

    [Function("SaveSelectedSites")]
    public async Task<IActionResult> SaveSelectedSites(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "v1/tenants/{id}/sites")] HttpRequest req,
        FunctionContext context,
        string id)
    {
        var mspOrgId = context.GetMspOrgId();
        if (string.IsNullOrEmpty(mspOrgId))
            return ResponseEnvelopeHelper.Unauthorized(context.InvocationId);

        if (!Guid.TryParse(id, out var tenantId))
            return ResponseEnvelopeHelper.BadRequest("Invalid tenant ID.", context.InvocationId);

        var body = await req.ReadFromJsonAsync<SaveSelectedSitesRequest>();
        if (body is null || body.SelectedSiteIds.Count == 0)
            return ResponseEnvelopeHelper.BadRequest("At least one site must be selected.", context.InvocationId);

        try
        {
            var result = await _service.SaveSelectedSitesAsync(tenantId, Guid.Parse(mspOrgId), body);
            return ResponseEnvelopeHelper.Ok(result);
        }
        catch (InvalidOperationException ex) when (ex.Message.Contains("not found", StringComparison.OrdinalIgnoreCase))
        {
            _logger.LogWarning(ex, "Tenant not found for saving sites: {TenantId}", tenantId);
            return ResponseEnvelopeHelper.NotFound(ex.Message, context.InvocationId);
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogWarning(ex, "Invalid tenant state for saving sites: {TenantId}", tenantId);
            return ResponseEnvelopeHelper.BadRequest(ex.Message, context.InvocationId);
        }
    }

    [Function("GetTenantSettings")]
    public async Task<IActionResult> GetTenantSettings(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "v1/tenants/{id}/settings")] HttpRequest req,
        FunctionContext context,
        string id)
    {
        var mspOrgId = context.GetMspOrgId();
        if (string.IsNullOrEmpty(mspOrgId))
            return ResponseEnvelopeHelper.Unauthorized(context.InvocationId);

        if (!Guid.TryParse(id, out var tenantId) || !Guid.TryParse(mspOrgId, out var parsedOrgId))
            return ResponseEnvelopeHelper.BadRequest("Invalid tenant or organization ID.", context.InvocationId);

        var tenant = await _db.ClientTenants
            .AsNoTracking()
            .Where(t => t.Id == tenantId && t.MspOrgId == parsedOrgId)
            .Select(t => new TenantSettingsDto
            {
                TenantId = t.Id,
                AutoApprovalDays = t.AutoApprovalDays,
                ReviewFlagged = t.ReviewFlagged,
            })
            .FirstOrDefaultAsync(req.HttpContext.RequestAborted);

        if (tenant is null)
            return ResponseEnvelopeHelper.NotFound("Tenant not found.", context.InvocationId);

        return ResponseEnvelopeHelper.Ok(tenant);
    }

    [Function("UpdateTenantSettings")]
    public async Task<IActionResult> UpdateTenantSettings(
        [HttpTrigger(AuthorizationLevel.Anonymous, "patch", Route = "v1/tenants/{id}/settings")] HttpRequest req,
        FunctionContext context,
        string id)
    {
        var mspOrgId = context.GetMspOrgId();
        if (string.IsNullOrEmpty(mspOrgId))
            return ResponseEnvelopeHelper.Unauthorized(context.InvocationId);

        if (!Guid.TryParse(id, out var tenantId) || !Guid.TryParse(mspOrgId, out var parsedOrgId))
            return ResponseEnvelopeHelper.BadRequest("Invalid tenant or organization ID.", context.InvocationId);

        var body = await req.ReadFromJsonAsync<UpdateTenantSettingsRequest>(req.HttpContext.RequestAborted);
        if (body is null)
            return ResponseEnvelopeHelper.BadRequest("Request body is required.", context.InvocationId);

        // Extract and validate AutoApprovalDays with PATCH semantics
        var (autoApprovalProvided, autoApprovalValue) = body.GetAutoApprovalDays();
        if (autoApprovalProvided && autoApprovalValue.HasValue
            && (autoApprovalValue.Value < 0 || autoApprovalValue.Value > 365))
            return ResponseEnvelopeHelper.BadRequest("AutoApprovalDays must be between 0 and 365, or null for never.", context.InvocationId);

        var tenant = await _db.ClientTenants
            .FirstOrDefaultAsync(t => t.Id == tenantId && t.MspOrgId == parsedOrgId, req.HttpContext.RequestAborted);

        if (tenant is null)
            return ResponseEnvelopeHelper.NotFound("Tenant not found.", context.InvocationId);

        // Only update fields that were explicitly provided in the request
        if (autoApprovalProvided)
            tenant.AutoApprovalDays = autoApprovalValue;

        await _db.SaveChangesAsync(req.HttpContext.RequestAborted);

        await _auditService.LogAsync(new AuditInput
        {
            MspOrgId = parsedOrgId,
            ClientTenantId = tenantId,
            Action = "TenantSettingsUpdated",
            Details = new { tenantId, autoApprovalDays = tenant.AutoApprovalDays, reviewFlagged = tenant.ReviewFlagged },
        }, req.HttpContext.RequestAborted);

        _logger.LogInformation(
            "Updated tenant {TenantId} settings: AutoApprovalDays={AutoApprovalDays}",
            tenantId, tenant.AutoApprovalDays);

        return ResponseEnvelopeHelper.Ok(new TenantSettingsDto
        {
            TenantId = tenant.Id,
            AutoApprovalDays = tenant.AutoApprovalDays,
            ReviewFlagged = tenant.ReviewFlagged,
        });
    }
}
