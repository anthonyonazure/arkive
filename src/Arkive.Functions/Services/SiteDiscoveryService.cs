using Arkive.Core.Configuration;
using Arkive.Core.DTOs;
using Arkive.Core.Interfaces;
using Azure.Identity;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.Graph;
using Microsoft.Graph.Models;
using Microsoft.Graph.Models.ODataErrors;

namespace Arkive.Functions.Services;

public class SiteDiscoveryService : ISiteDiscoveryService
{
    private readonly EntraIdOptions _entraIdOptions;
    private readonly IKeyVaultService _keyVaultService;
    private readonly ILogger<SiteDiscoveryService> _logger;

    private const string ClientSecretName = "arkive-client-secret";

    public SiteDiscoveryService(
        IOptions<EntraIdOptions> entraIdOptions,
        IKeyVaultService keyVaultService,
        ILogger<SiteDiscoveryService> logger)
    {
        _entraIdOptions = entraIdOptions.Value;
        _keyVaultService = keyVaultService;
        _logger = logger;
    }

    public async Task<IReadOnlyList<SharePointSiteDto>> DiscoverSitesAsync(string m365TenantId, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(m365TenantId);

        var graphClient = await CreateGraphClientAsync(m365TenantId, cancellationToken);

        _logger.LogInformation("Discovering SharePoint sites for tenant {M365TenantId}", m365TenantId);

        var allSites = await FetchAllSitesAsync(graphClient, cancellationToken);

        // Filter out personal OneDrive sites and subsites
        var filteredSites = allSites
            .Where(s => s.SiteCollection is not null)
            .Where(s => s.WebUrl is not null && !s.WebUrl.Contains("/personal/", StringComparison.OrdinalIgnoreCase))
            .ToList();

        _logger.LogInformation("Found {TotalCount} total sites, {FilteredCount} after filtering for tenant {M365TenantId}",
            allSites.Count, filteredSites.Count, m365TenantId);

        var results = new List<SharePointSiteDto>(filteredSites.Count);

        foreach (var site in filteredSites)
        {
            long storageUsedBytes = 0;
            try
            {
                var drive = await graphClient.Sites[site.Id].Drive.GetAsync(config =>
                {
                    config.QueryParameters.Select = ["id", "quota"];
                }, cancellationToken);

                storageUsedBytes = drive?.Quota?.Used ?? 0;
            }
            catch (ODataError ex)
            {
                _logger.LogWarning(ex, "Failed to get storage quota for site {SiteId}. Defaulting to 0.", site.Id);
            }

            results.Add(new SharePointSiteDto
            {
                SiteId = site.Id ?? string.Empty,
                Url = site.WebUrl ?? string.Empty,
                DisplayName = site.DisplayName ?? string.Empty,
                StorageUsedBytes = storageUsedBytes
            });
        }

        return results;
    }

    private async Task<GraphServiceClient> CreateGraphClientAsync(string m365TenantId, CancellationToken cancellationToken)
    {
        var clientSecret = await _keyVaultService.GetSecretAsync(ClientSecretName, cancellationToken);

        var credential = new ClientSecretCredential(
            m365TenantId,
            _entraIdOptions.ClientId,
            clientSecret,
            new ClientSecretCredentialOptions { AuthorityHost = AzureAuthorityHosts.AzurePublicCloud });

        var scopes = new[] { "https://graph.microsoft.com/.default" };
        return new GraphServiceClient(credential, scopes);
    }

    private static async Task<List<Site>> FetchAllSitesAsync(GraphServiceClient graphClient, CancellationToken cancellationToken)
    {
        var allSites = new List<Site>();

        var page = await graphClient.Sites.GetAsync(config =>
        {
            config.QueryParameters.Search = "*";
            config.QueryParameters.Select = ["id", "displayName", "webUrl", "siteCollection"];
        }, cancellationToken);

        while (page is not null)
        {
            allSites.AddRange(page.Value ?? []);

            if (page.OdataNextLink is not null)
            {
                page = await graphClient.Sites.WithUrl(page.OdataNextLink).GetAsync(cancellationToken: cancellationToken);
            }
            else
            {
                break;
            }
        }

        return allSites;
    }
}
