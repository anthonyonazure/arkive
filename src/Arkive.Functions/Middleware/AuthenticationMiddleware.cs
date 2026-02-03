using System.IdentityModel.Tokens.Jwt;
using System.Text.Json;
using Arkive.Core.Configuration;
using Arkive.Functions.Api;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Middleware;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Protocols;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using Microsoft.IdentityModel.Tokens;

namespace Arkive.Functions.Middleware;

public class AuthenticationMiddleware : IFunctionsWorkerMiddleware
{
    private readonly ILogger<AuthenticationMiddleware> _logger;
    private readonly EntraIdOptions _entraIdOptions;
    private readonly ConfigurationManager<OpenIdConnectConfiguration> _configManager;
    private readonly JwtSecurityTokenHandler _tokenHandler;

    private static readonly HashSet<string> AnonymousEndpoints = new(StringComparer.OrdinalIgnoreCase)
    {
        nameof(HealthEndpoints.Health),
        nameof(ReportEndpoints.GetSharedReport),
    };

    public AuthenticationMiddleware(
        ILogger<AuthenticationMiddleware> logger,
        IOptions<EntraIdOptions> entraIdOptions)
    {
        _logger = logger;
        _entraIdOptions = entraIdOptions.Value;

        var metadataAddress = $"{_entraIdOptions.Instance}{_entraIdOptions.TenantId}/v2.0/.well-known/openid-configuration";
        _configManager = new ConfigurationManager<OpenIdConnectConfiguration>(
            metadataAddress,
            new OpenIdConnectConfigurationRetriever(),
            new HttpDocumentRetriever());

        _tokenHandler = new JwtSecurityTokenHandler();
    }

    public async Task Invoke(FunctionContext context, FunctionExecutionDelegate next)
    {
        var functionName = context.FunctionDefinition.Name;

        if (IsAnonymousEndpoint(functionName))
        {
            await next(context);
            return;
        }

        var token = ExtractBearerToken(context);
        if (string.IsNullOrEmpty(token))
        {
            _logger.LogWarning("Missing or invalid Authorization header for function {FunctionName}", functionName);
            await WriteUnauthorizedResponseAsync(context, "Missing or invalid Authorization header.");
            return;
        }

        try
        {
            var openIdConfig = await _configManager.GetConfigurationAsync(CancellationToken.None);

            var validationParameters = new TokenValidationParameters
            {
                ValidateIssuer = true,
                ValidIssuer = $"{_entraIdOptions.Instance}{_entraIdOptions.TenantId}/v2.0",
                ValidateAudience = true,
                ValidAudiences = new[] { _entraIdOptions.Audience, $"api://{_entraIdOptions.ClientId}" },
                ValidateIssuerSigningKey = true,
                IssuerSigningKeys = openIdConfig.SigningKeys,
                ValidateLifetime = true,
                ClockSkew = TimeSpan.FromMinutes(2)
            };

            var result = await _tokenHandler.ValidateTokenAsync(token, validationParameters);

            if (!result.IsValid)
            {
                _logger.LogWarning("Token validation failed for function {FunctionName}: {Error}",
                    functionName, result.Exception?.Message);
                await WriteUnauthorizedResponseAsync(context, "Invalid token.");
                return;
            }

            SetClaimsOnContext(context, result);

            await next(context);
        }
        catch (SecurityTokenException ex)
        {
            _logger.LogWarning(ex, "Security token exception for function {FunctionName}", functionName);
            await WriteUnauthorizedResponseAsync(context, "Invalid token.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error during authentication for function {FunctionName}", functionName);
            await WriteUnauthorizedResponseAsync(context, "Authentication failed.");
        }
    }

    private static bool IsAnonymousEndpoint(string functionName)
    {
        return AnonymousEndpoints.Contains(functionName);
    }

    private static string? ExtractBearerToken(FunctionContext context)
    {
        // With ASP.NET Core integration, access HttpContext for headers
        var httpContext = context.GetHttpContext();
        if (httpContext is not null)
        {
            var authHeader = httpContext.Request.Headers.Authorization.ToString();
            if (!string.IsNullOrEmpty(authHeader) && authHeader.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
            {
                return authHeader["Bearer ".Length..].Trim();
            }
            return null;
        }

        // Fallback: extract from binding data (non-ASP.NET Core HTTP triggers)
        if (!context.BindingContext.BindingData.TryGetValue("Headers", out var headersObj))
            return null;

        var headersJson = headersObj?.ToString();
        if (string.IsNullOrEmpty(headersJson))
            return null;

        try
        {
            var headers = JsonSerializer.Deserialize<Dictionary<string, string>>(headersJson,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

            if (headers is null || !headers.TryGetValue("Authorization", out var authHeaderValue))
                return null;

            if (!authHeaderValue.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
                return null;

            return authHeaderValue["Bearer ".Length..].Trim();
        }
        catch
        {
            return null;
        }
    }

    private static void SetClaimsOnContext(FunctionContext context, TokenValidationResult result)
    {
        var claims = result.ClaimsIdentity;

        var objectId = claims.FindFirst(Core.Constants.ArkiveClaimTypes.ObjectId)?.Value ?? string.Empty;
        var tenantId = claims.FindFirst(Core.Constants.ArkiveClaimTypes.TenantId)?.Value ?? string.Empty;

        context.Items["UserId"] = objectId;
        context.Items["EntraObjectId"] = objectId;
        context.Items["TenantId"] = tenantId;
        context.Items["UserName"] = claims.FindFirst(Core.Constants.ArkiveClaimTypes.Name)?.Value ?? string.Empty;
        context.Items["UserEmail"] = claims.FindFirst(Core.Constants.ArkiveClaimTypes.PreferredUsername)?.Value ?? string.Empty;

        // Use extension_MspOrgId claim if present, otherwise fall back to Entra TenantId
        var mspOrgId = claims.FindFirst(Core.Constants.ArkiveClaimTypes.MspOrgId)?.Value;
        context.Items["MspOrgId"] = !string.IsNullOrEmpty(mspOrgId) ? mspOrgId : tenantId;

        var roles = claims.FindAll(Core.Constants.ArkiveClaimTypes.Roles)
            .Select(c => c.Value)
            .ToList();
        context.Items["UserRoles"] = roles;
        context.Items["UserRole"] = roles.FirstOrDefault() ?? string.Empty;
    }

    private static Task WriteUnauthorizedResponseAsync(FunctionContext context, string message)
    {
        context.GetInvocationResult().Value =
            ResponseEnvelopeHelper.Error(
                System.Net.HttpStatusCode.Unauthorized,
                "UNAUTHORIZED",
                message,
                context.InvocationId);

        return Task.CompletedTask;
    }
}
