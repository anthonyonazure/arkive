using Arkive.Core.Models;
using Arkive.Functions.Extensions;
using Arkive.Functions.Middleware;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;

namespace Arkive.Functions.Api;

public class HealthEndpoints
{
    [Function("Health")]
    public IActionResult Health(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "health")] HttpRequest req)
    {
        return new OkObjectResult(new { status = "healthy" });
    }

    [Function("GetCurrentUser")]
    public IActionResult GetCurrentUser(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "v1/me")] HttpRequest req,
        FunctionContext context)
    {
        var mspOrgId = context.GetMspOrgId();

        var profile = new UserProfile
        {
            EntraObjectId = context.GetEntraObjectId(),
            MspOrgId = mspOrgId,
            Name = context.GetUserName(),
            Email = context.GetUserEmail(),
            Role = context.GetUserRole(),
            Roles = context.GetUserRoles()
        };

        return ResponseEnvelopeHelper.Ok(profile);
    }
}
