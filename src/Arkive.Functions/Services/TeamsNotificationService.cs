using AdaptiveCards;
using Arkive.Core.DTOs;
using Arkive.Core.Interfaces;
using Microsoft.Bot.Connector;
using Microsoft.Bot.Connector.Authentication;
using Microsoft.Bot.Schema;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;

namespace Arkive.Functions.Services;

public class TeamsNotificationService : ITeamsNotificationService
{
    private readonly ILogger<TeamsNotificationService> _logger;
    private readonly string _appId;
    private readonly string _appPassword;
    private readonly string _serviceUrl;

    private const int MaxRetries = 3;
    private static readonly TimeSpan InitialRetryDelay = TimeSpan.FromSeconds(10);

    public TeamsNotificationService(
        IConfiguration configuration,
        ILogger<TeamsNotificationService> logger)
    {
        _logger = logger;
        _appId = configuration["BotFramework:AppId"] ?? string.Empty;
        _appPassword = configuration["BotFramework:AppPassword"] ?? string.Empty;
        _serviceUrl = configuration["BotFramework:ServiceUrl"]
            ?? "https://smba.trafficmanager.net/amer/";
    }

    public async Task<ApprovalNotificationResult> SendApprovalCardAsync(
        ApprovalNotificationInput input,
        CancellationToken cancellationToken = default)
    {
        var result = new ApprovalNotificationResult
        {
            SiteOwnerEmail = input.SiteOwnerEmail,
            SiteId = input.SiteId,
        };

        if (string.IsNullOrEmpty(_appId) || string.IsNullOrEmpty(_appPassword))
        {
            _logger.LogWarning(
                "Bot Framework credentials not configured — skipping notification for {SiteOwner}",
                input.SiteOwnerEmail);
            result.Delivered = false;
            result.ErrorMessage = "Bot Framework credentials not configured";
            result.AttemptCount = 0;
            return result;
        }

        if (string.IsNullOrEmpty(input.SiteOwnerAadId) && string.IsNullOrEmpty(input.SiteOwnerEmail))
        {
            _logger.LogWarning(
                "Neither site owner AAD ID nor email is available — cannot send Teams notification");
            result.Delivered = false;
            result.ErrorMessage = "No site owner identifier available";
            result.AttemptCount = 0;
            return result;
        }

        var card = BuildApprovalCard(input);
        var cardAttachment = new Attachment
        {
            ContentType = AdaptiveCard.ContentType,
            Content = JsonConvert.DeserializeObject(card.ToJson()),
        };

        // Create credentials and client once, reuse across retry attempts
        var credentials = new MicrosoftAppCredentials(_appId, _appPassword);
        using var connectorClient = new ConnectorClient(
            new Uri(_serviceUrl), credentials);

        // Use AAD object ID if available, otherwise fall back to UPN (email)
        var memberId = !string.IsNullOrEmpty(input.SiteOwnerAadId)
            ? $"29:{input.SiteOwnerAadId}"
            : input.SiteOwnerEmail;

        // Retry with exponential backoff (10s, 20s, 40s)
        for (var attempt = 1; attempt <= MaxRetries; attempt++)
        {
            result.AttemptCount = attempt;

            try
            {
                // Create 1:1 conversation with the site owner
                var conversationParams = new ConversationParameters
                {
                    Bot = new ChannelAccount { Id = $"28:{_appId}" },
                    Members = [new ChannelAccount { Id = memberId }],
                    TenantId = input.M365TenantId,
                    IsGroup = false,
                };

                var conversationResource = await connectorClient.Conversations
                    .CreateConversationAsync(conversationParams, cancellationToken);

                // Send the Adaptive Card
                var activity = Activity.CreateMessageActivity();
                activity.Attachments = [cardAttachment];

                await connectorClient.Conversations.SendToConversationAsync(
                    conversationResource.Id,
                    (Activity)activity,
                    cancellationToken);

                result.Delivered = true;
                result.ConversationId = conversationResource.Id;

                _logger.LogInformation(
                    "Approval card sent to {SiteOwner} for site {SiteName} ({FileCount} files, {SizeBytes} bytes)",
                    input.SiteOwnerEmail, input.SiteName, input.FileCount, input.TotalSizeBytes);

                return result;
            }
            catch (Exception ex) when (attempt < MaxRetries)
            {
                var delay = InitialRetryDelay * Math.Pow(2, attempt - 1);
                _logger.LogWarning(ex,
                    "Attempt {Attempt}/{MaxRetries} failed to send approval card to {SiteOwner}. Retrying in {DelaySeconds}s",
                    attempt, MaxRetries, input.SiteOwnerEmail, delay.TotalSeconds);

                await Task.Delay(delay, cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex,
                    "All {MaxRetries} attempts failed to send approval card to {SiteOwner} for site {SiteName}",
                    MaxRetries, input.SiteOwnerEmail, input.SiteName);

                result.Delivered = false;
                result.ErrorMessage = ex.Message;
            }
        }

        return result;
    }

    private static AdaptiveCard BuildApprovalCard(ApprovalNotificationInput input)
    {
        var storageMb = input.TotalSizeBytes / (1024.0 * 1024.0);
        var storageDisplay = storageMb >= 1024
            ? $"{storageMb / 1024:F1} GB"
            : $"{storageMb:F1} MB";

        var card = new AdaptiveCard(new AdaptiveSchemaVersion(1, 4))
        {
            Body =
            [
                new AdaptiveTextBlock
                {
                    Text = "Archive Approval Request",
                    Size = AdaptiveTextSize.Large,
                    Weight = AdaptiveTextWeight.Bolder,
                },
                new AdaptiveTextBlock
                {
                    Text = $"Arkive proposes to archive files from **{input.SiteName}** to **{input.TargetTier}** tier storage.",
                    Wrap = true,
                },
                new AdaptiveFactSet
                {
                    Facts =
                    [
                        new AdaptiveFact("Site", input.SiteName),
                        new AdaptiveFact("Files Affected", input.FileCount.ToString("N0")),
                        new AdaptiveFact("Storage", storageDisplay),
                        new AdaptiveFact("Target Tier", input.TargetTier),
                    ],
                },
                new AdaptiveTextBlock
                {
                    Text = "Files can be restored from archive at any time. Approve to proceed, reject to keep files in place, or request a review.",
                    Wrap = true,
                    Size = AdaptiveTextSize.Small,
                    IsSubtle = true,
                },
                new AdaptiveTextInput
                {
                    Id = "reason",
                    Placeholder = "Optional: reason for rejection or review request",
                    IsMultiline = true,
                    MaxLength = 500,
                },
            ],
            Actions =
            [
                new AdaptiveSubmitAction
                {
                    Title = "Approve",
                    Data = new
                    {
                        action = "approve",
                        tenantId = input.TenantId.ToString(),
                        mspOrgId = input.MspOrgId.ToString(),
                        siteId = input.SiteId,
                        orchestrationInstanceId = input.OrchestrationInstanceId,
                    },
                    Style = "positive",
                },
                new AdaptiveSubmitAction
                {
                    Title = "Reject",
                    Data = new
                    {
                        action = "reject",
                        tenantId = input.TenantId.ToString(),
                        mspOrgId = input.MspOrgId.ToString(),
                        siteId = input.SiteId,
                        orchestrationInstanceId = input.OrchestrationInstanceId,
                    },
                    Style = "destructive",
                },
                new AdaptiveSubmitAction
                {
                    Title = "Request Review",
                    Data = new
                    {
                        action = "review",
                        tenantId = input.TenantId.ToString(),
                        mspOrgId = input.MspOrgId.ToString(),
                        siteId = input.SiteId,
                        orchestrationInstanceId = input.OrchestrationInstanceId,
                    },
                },
            ],
        };

        return card;
    }
}
