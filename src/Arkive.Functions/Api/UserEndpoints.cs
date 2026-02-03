using Arkive.Core.Constants;
using Arkive.Core.DTOs;
using Arkive.Core.Enums;
using Arkive.Core.Interfaces;
using Arkive.Functions.Extensions;
using Arkive.Functions.Middleware;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Arkive.Functions.Api;

public class UserEndpoints
{
    private readonly IUserService _service;
    private readonly ILogger<UserEndpoints> _logger;

    public UserEndpoints(IUserService service, ILogger<UserEndpoints> logger)
    {
        _service = service;
        _logger = logger;
    }

    [Function("CreateUser")]
    public async Task<IActionResult> CreateUser(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "v1/users")] HttpRequest req,
        FunctionContext context)
    {
        var role = context.GetUserRole();
        if (role != AppRoles.MspAdmin && role != AppRoles.PlatformAdmin)
            return ResponseEnvelopeHelper.Forbidden(context.InvocationId);

        var body = await req.ReadFromJsonAsync<CreateUserRequest>();
        if (body is null)
            return ResponseEnvelopeHelper.BadRequest("Request body is required.", context.InvocationId);

        if (string.IsNullOrWhiteSpace(body.EntraIdObjectId) || body.EntraIdObjectId.Length > 36)
            return ResponseEnvelopeHelper.BadRequest("EntraIdObjectId is required and must not exceed 36 characters.", context.InvocationId);

        if (string.IsNullOrWhiteSpace(body.Email) || body.Email.Length > 320)
            return ResponseEnvelopeHelper.BadRequest("Email is required and must not exceed 320 characters.", context.InvocationId);

        if (string.IsNullOrWhiteSpace(body.DisplayName) || body.DisplayName.Length > 200)
            return ResponseEnvelopeHelper.BadRequest("DisplayName is required and must not exceed 200 characters.", context.InvocationId);

        if (!Enum.IsDefined(typeof(UserRole), body.Role))
            return ResponseEnvelopeHelper.BadRequest("Invalid role specified.", context.InvocationId);

        // Prevent role escalation: MspAdmin cannot create PlatformAdmin users
        if (role == AppRoles.MspAdmin && body.Role == UserRole.PlatformAdmin)
            return ResponseEnvelopeHelper.Forbidden(context.InvocationId);

        var mspOrgIdStr = context.GetMspOrgId();
        if (!Guid.TryParse(mspOrgIdStr, out var mspOrgId))
            return ResponseEnvelopeHelper.BadRequest("Invalid organization context.", context.InvocationId);

        try
        {
            var result = await _service.CreateAsync(body, mspOrgId);
            return ResponseEnvelopeHelper.Created($"/api/v1/users/{result.Id}", result);
        }
        catch (DbUpdateException ex)
        {
            _logger.LogWarning(ex, "Database constraint violation creating user {UserEmail}", body.Email);
            return ResponseEnvelopeHelper.Conflict("A user with this Entra ID object ID already exists.", context.InvocationId);
        }
    }

    [Function("ListUsers")]
    public async Task<IActionResult> ListUsers(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "v1/users")] HttpRequest req,
        FunctionContext context)
    {
        var role = context.GetUserRole();
        if (role != AppRoles.MspAdmin && role != AppRoles.PlatformAdmin)
            return ResponseEnvelopeHelper.Forbidden(context.InvocationId);

        if (role == AppRoles.PlatformAdmin)
        {
            var allUsers = await _service.GetAllAsync();
            return ResponseEnvelopeHelper.Ok(allUsers);
        }

        var mspOrgIdStr = context.GetMspOrgId();
        if (!Guid.TryParse(mspOrgIdStr, out var mspOrgId))
            return ResponseEnvelopeHelper.BadRequest("Invalid organization context.", context.InvocationId);

        var users = await _service.GetAllByOrgAsync(mspOrgId);
        return ResponseEnvelopeHelper.Ok(users);
    }

    [Function("GetUser")]
    public async Task<IActionResult> GetUser(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "v1/users/{id}")] HttpRequest req,
        FunctionContext context,
        string id)
    {
        var role = context.GetUserRole();
        if (role != AppRoles.MspAdmin && role != AppRoles.PlatformAdmin)
            return ResponseEnvelopeHelper.Forbidden(context.InvocationId);

        if (!Guid.TryParse(id, out var userId))
            return ResponseEnvelopeHelper.BadRequest("Invalid user ID.", context.InvocationId);

        var user = await _service.GetByIdAsync(userId);
        if (user is null)
            return ResponseEnvelopeHelper.NotFound("User not found.", context.InvocationId);

        return ResponseEnvelopeHelper.Ok(user);
    }

    [Function("UpdateUser")]
    public async Task<IActionResult> UpdateUser(
        [HttpTrigger(AuthorizationLevel.Anonymous, "put", Route = "v1/users/{id}")] HttpRequest req,
        FunctionContext context,
        string id)
    {
        var role = context.GetUserRole();
        if (role != AppRoles.MspAdmin && role != AppRoles.PlatformAdmin)
            return ResponseEnvelopeHelper.Forbidden(context.InvocationId);

        if (!Guid.TryParse(id, out var userId))
            return ResponseEnvelopeHelper.BadRequest("Invalid user ID.", context.InvocationId);

        var body = await req.ReadFromJsonAsync<UpdateUserRequest>();
        if (body is null)
            return ResponseEnvelopeHelper.BadRequest("Request body is required.", context.InvocationId);

        if (!Enum.IsDefined(typeof(UserRole), body.Role))
            return ResponseEnvelopeHelper.BadRequest("Invalid role specified.", context.InvocationId);

        // Prevent role escalation: MspAdmin cannot assign PlatformAdmin role
        if (role == AppRoles.MspAdmin && body.Role == UserRole.PlatformAdmin)
            return ResponseEnvelopeHelper.Forbidden(context.InvocationId);

        var user = await _service.UpdateRoleAsync(userId, body);
        if (user is null)
            return ResponseEnvelopeHelper.NotFound("User not found.", context.InvocationId);

        return ResponseEnvelopeHelper.Ok(user);
    }

    [Function("DeleteUser")]
    public async Task<IActionResult> DeleteUser(
        [HttpTrigger(AuthorizationLevel.Anonymous, "delete", Route = "v1/users/{id}")] HttpRequest req,
        FunctionContext context,
        string id)
    {
        var role = context.GetUserRole();
        if (role != AppRoles.MspAdmin && role != AppRoles.PlatformAdmin)
            return ResponseEnvelopeHelper.Forbidden(context.InvocationId);

        if (!Guid.TryParse(id, out var userId))
            return ResponseEnvelopeHelper.BadRequest("Invalid user ID.", context.InvocationId);

        var deleted = await _service.DeleteAsync(userId);
        if (!deleted)
            return ResponseEnvelopeHelper.NotFound("User not found.", context.InvocationId);

        return ResponseEnvelopeHelper.NoContent();
    }
}
