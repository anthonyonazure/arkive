using Arkive.Core.Constants;
using Arkive.Core.DTOs;
using Arkive.Core.Interfaces;
using Arkive.Functions.Extensions;
using Arkive.Functions.Middleware;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Arkive.Functions.Api;

public class OrganizationEndpoints
{
    private readonly IMspOrganizationService _service;
    private readonly ILogger<OrganizationEndpoints> _logger;

    public OrganizationEndpoints(IMspOrganizationService service, ILogger<OrganizationEndpoints> logger)
    {
        _service = service;
        _logger = logger;
    }

    [Function("CreateOrganization")]
    public async Task<IActionResult> CreateOrganization(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "v1/organizations")] HttpRequest req,
        FunctionContext context)
    {
        if (context.GetUserRole() != AppRoles.PlatformAdmin)
            return ResponseEnvelopeHelper.Forbidden(context.InvocationId);

        var body = await req.ReadFromJsonAsync<CreateMspOrganizationRequest>();
        if (body is null)
            return ResponseEnvelopeHelper.BadRequest("Request body is required.", context.InvocationId);

        if (string.IsNullOrWhiteSpace(body.Name) || body.Name.Length > 200)
            return ResponseEnvelopeHelper.BadRequest("Name is required and must not exceed 200 characters.", context.InvocationId);

        if (string.IsNullOrWhiteSpace(body.EntraIdTenantId) || body.EntraIdTenantId.Length > 36)
            return ResponseEnvelopeHelper.BadRequest("EntraIdTenantId is required and must not exceed 36 characters.", context.InvocationId);

        try
        {
            var result = await _service.CreateAsync(body);
            return ResponseEnvelopeHelper.Created($"/api/v1/organizations/{result.Id}", result);
        }
        catch (DbUpdateException ex)
        {
            _logger.LogWarning(ex, "Database constraint violation creating organization {OrgName}", body.Name);
            return ResponseEnvelopeHelper.Conflict("An organization with this Entra ID tenant already exists.", context.InvocationId);
        }
    }

    [Function("ListOrganizations")]
    public async Task<IActionResult> ListOrganizations(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "v1/organizations")] HttpRequest req,
        FunctionContext context)
    {
        if (context.GetUserRole() != AppRoles.PlatformAdmin)
            return ResponseEnvelopeHelper.Forbidden(context.InvocationId);

        var orgs = await _service.GetAllAsync();
        return ResponseEnvelopeHelper.Ok(orgs);
    }
}
