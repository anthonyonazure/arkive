using System.Text.Json;
using Arkive.Core.DTOs;
using Arkive.Core.Interfaces;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;

namespace Arkive.Functions.Api;

/// <summary>
/// Bot Framework messaging endpoint for receiving Teams Adaptive Card responses.
/// Teams posts card action responses (approve/reject/review) to this endpoint.
/// </summary>
public class BotEndpoints
{
    private readonly IApprovalActionHandler _approvalHandler;
    private readonly ILogger<BotEndpoints> _logger;

    public BotEndpoints(
        IApprovalActionHandler approvalHandler,
        ILogger<BotEndpoints> logger)
    {
        _approvalHandler = approvalHandler;
        _logger = logger;
    }

    [Function("BotMessages")]
    public async Task<IActionResult> HandleBotMessages(
        [HttpTrigger(AuthorizationLevel.Function, "post", Route = "bot/messages")]
        HttpRequest req)
    {
        // Read the incoming activity from Teams
        using var reader = new StreamReader(req.Body);
        var body = await reader.ReadToEndAsync();

        _logger.LogInformation("Received bot message: {BodyLength} bytes", body.Length);

        if (string.IsNullOrEmpty(body))
            return new OkObjectResult(new { status = "empty" });

        try
        {
            // Deserialize the Bot Framework Activity
            using var doc = JsonDocument.Parse(body);
            var root = doc.RootElement;

            var activityType = root.TryGetProperty("type", out var typeProp)
                ? typeProp.GetString() : null;

            // Only process message activities with value (Action.Submit responses)
            if (activityType != "message" || !root.TryGetProperty("value", out var valueProp))
            {
                _logger.LogDebug("Ignoring activity type {ActivityType} (not an Action.Submit)", activityType);
                return new OkObjectResult(new { status = "ignored" });
            }

            // Extract action data from the card submit
            var action = valueProp.TryGetProperty("action", out var actionProp)
                ? actionProp.GetString() : null;
            var tenantId = valueProp.TryGetProperty("tenantId", out var tenantProp)
                ? tenantProp.GetString() : null;
            var siteId = valueProp.TryGetProperty("siteId", out var siteProp)
                ? siteProp.GetString() : null;
            var orchestrationInstanceId = valueProp.TryGetProperty("orchestrationInstanceId", out var orchProp)
                ? orchProp.GetString() : null;
            var mspOrgId = valueProp.TryGetProperty("mspOrgId", out var orgProp)
                ? orgProp.GetString() : null;
            var reason = valueProp.TryGetProperty("reason", out var reasonProp)
                ? reasonProp.GetString() : null;

            if (string.IsNullOrEmpty(action) || string.IsNullOrEmpty(tenantId) || string.IsNullOrEmpty(siteId))
            {
                _logger.LogWarning("Missing required fields in card action data");
                return new BadRequestObjectResult(new { error = "Missing required action data" });
            }

            // Extract actor identity from the activity
            // Teams from.id may contain UPN-style identifier; from.aadObjectId is a GUID (not email)
            var actorEmail = string.Empty;
            var actorName = string.Empty;
            if (root.TryGetProperty("from", out var fromProp))
            {
                actorName = fromProp.TryGetProperty("name", out var nameProp)
                    ? nameProp.GetString() ?? string.Empty : string.Empty;

                // from.id in Teams 1:1 chats often contains the user's UPN or email-like identifier
                if (fromProp.TryGetProperty("id", out var idProp))
                {
                    var fromId = idProp.GetString() ?? string.Empty;
                    if (fromId.Contains('@'))
                        actorEmail = fromId;
                }

                // Fall back to display name if no email-like ID found
                if (string.IsNullOrEmpty(actorEmail))
                    actorEmail = actorName;
            }

            var actionInput = new ApprovalActionInput
            {
                Action = action,
                TenantId = tenantId,
                MspOrgId = mspOrgId ?? string.Empty,
                SiteId = siteId,
                OrchestrationInstanceId = orchestrationInstanceId ?? string.Empty,
                Reason = reason,
                ActorEmail = actorEmail,
                ActorName = actorName,
            };

            var result = await _approvalHandler.HandleActionAsync(actionInput, req.HttpContext.RequestAborted);

            _logger.LogInformation(
                "Processed {Action} action for site {SiteId}: Success={Success}, Message={Message}",
                action, siteId, result.Success, result.Message);

            return new OkObjectResult(new { status = result.Success ? "processed" : "failed", result.Message });
        }
        catch (JsonException ex)
        {
            _logger.LogWarning(ex, "Failed to parse bot message body");
            return new BadRequestObjectResult(new { error = "Invalid message format" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing bot message");
            return new ObjectResult(new { error = "Internal error processing action" })
            {
                StatusCode = 500,
            };
        }
    }
}
