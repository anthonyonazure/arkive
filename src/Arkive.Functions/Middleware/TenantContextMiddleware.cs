using Arkive.Data;
using Arkive.Data.Extensions;
using Arkive.Functions.Api;
using Arkive.Functions.Extensions;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Middleware;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Arkive.Functions.Middleware;

/// <summary>
/// Middleware that extracts tenant identifiers from the authenticated user's claims
/// and sets them on the scoped TenantContext for RLS SESSION_CONTEXT injection.
/// Must run AFTER AuthenticationMiddleware.
/// </summary>
public class TenantContextMiddleware : IFunctionsWorkerMiddleware
{
    private readonly ILogger<TenantContextMiddleware> _logger;

    private static readonly HashSet<string> AnonymousEndpoints = new(StringComparer.OrdinalIgnoreCase)
    {
        nameof(HealthEndpoints.Health)
    };

    public TenantContextMiddleware(ILogger<TenantContextMiddleware> logger)
    {
        _logger = logger;
    }

    public async Task Invoke(FunctionContext context, FunctionExecutionDelegate next)
    {
        var functionName = context.FunctionDefinition.Name;

        if (AnonymousEndpoints.Contains(functionName))
        {
            await next(context);
            return;
        }

        var mspOrgId = context.GetMspOrgId();
        if (!string.IsNullOrEmpty(mspOrgId))
        {
            var tenantContext = context.InstanceServices.GetRequiredService<TenantContext>();

            // Extract ClientTenantId from route/query if present
            string? clientTenantId = null;
            var httpContext = context.GetHttpContext();
            if (httpContext is not null)
            {
                clientTenantId = httpContext.Request.RouteValues["tenantId"]?.ToString()
                    ?? httpContext.Request.Query["tenantId"].FirstOrDefault();
            }

            tenantContext.SetFromClaims(mspOrgId, clientTenantId);

            _logger.LogDebug(
                "Tenant context set for function {FunctionName}: MspOrgId={MspOrgId}, ClientTenantId={ClientTenantId}",
                functionName, mspOrgId, clientTenantId ?? "(none)");
        }

        await next(context);
    }
}
