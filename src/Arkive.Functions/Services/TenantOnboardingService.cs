using System.Text.Json;
using Arkive.Core.DTOs;
using Arkive.Core.Enums;
using Arkive.Core.Interfaces;
using Arkive.Core.Models;
using Arkive.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Arkive.Functions.Services;

public class TenantOnboardingService : ITenantOnboardingService
{
    private readonly ArkiveDbContext _dbContext;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ISiteDiscoveryService _siteDiscoveryService;
    private readonly IKeyVaultService _keyVaultService;
    private readonly ILogger<TenantOnboardingService> _logger;

    public TenantOnboardingService(
        ArkiveDbContext dbContext,
        IHttpClientFactory httpClientFactory,
        ISiteDiscoveryService siteDiscoveryService,
        IKeyVaultService keyVaultService,
        ILogger<TenantOnboardingService> logger)
    {
        _dbContext = dbContext;
        _httpClientFactory = httpClientFactory;
        _siteDiscoveryService = siteDiscoveryService;
        _keyVaultService = keyVaultService;
        _logger = logger;
    }

    public async Task<ValidateDomainResponse> ValidateTenantDomainAsync(string domain, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(domain);

        var client = _httpClientFactory.CreateClient();
        var url = $"https://login.microsoftonline.com/{Uri.EscapeDataString(domain)}/.well-known/openid-configuration";

        try
        {
            var response = await client.GetAsync(url, cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogInformation("Domain validation failed for {Domain}: {StatusCode}", domain, response.StatusCode);
                return new ValidateDomainResponse { IsValid = false };
            }

            var json = await response.Content.ReadAsStringAsync(cancellationToken);
            using var doc = JsonDocument.Parse(json);

            var issuer = doc.RootElement.GetProperty("issuer").GetString() ?? string.Empty;
            // issuer format: https://sts.windows.net/{tenant-id}/
            var tenantId = ExtractTenantIdFromIssuer(issuer);

            _logger.LogInformation("Domain {Domain} validated successfully. TenantId: {TenantId}", domain, tenantId);

            return new ValidateDomainResponse
            {
                TenantId = tenantId,
                DisplayName = domain,
                IsValid = true
            };
        }
        catch (HttpRequestException ex)
        {
            _logger.LogWarning(ex, "Failed to validate domain {Domain}", domain);
            return new ValidateDomainResponse { IsValid = false };
        }
        catch (JsonException ex)
        {
            _logger.LogWarning(ex, "Failed to parse OpenID configuration for domain {Domain}", domain);
            return new ValidateDomainResponse { IsValid = false };
        }
    }

    public async Task<TenantDto> CreateTenantAsync(Guid mspOrgId, CreateTenantRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentException.ThrowIfNullOrEmpty(request.M365TenantId);
        ArgumentException.ThrowIfNullOrEmpty(request.DisplayName);

        // If a tenant already exists for this M365 tenant in a retryable state, return it
        var existing = await _dbContext.ClientTenants
            .FirstOrDefaultAsync(t => t.MspOrgId == mspOrgId && t.M365TenantId == request.M365TenantId, cancellationToken);

        if (existing is not null)
        {
            if (existing.Status is TenantStatus.Disconnected)
            {
                // Disconnected tenants can be re-onboarded: reset to Pending
                existing.DisplayName = request.DisplayName;
                existing.Status = TenantStatus.Pending;
                existing.ConnectedAt = null;
                await _dbContext.SaveChangesAsync(cancellationToken);
            }
            else if (existing.Status is TenantStatus.Pending or TenantStatus.Error)
            {
                existing.DisplayName = request.DisplayName;
                existing.Status = TenantStatus.Pending;
                await _dbContext.SaveChangesAsync(cancellationToken);
            }

            // Return existing tenant in any state so the wizard can resume
            _logger.LogInformation("Returning existing tenant {TenantId} (status: {Status}) for onboarding", existing.Id, existing.Status);
            return MapToDto(existing);
        }

        var entity = new ClientTenant
        {
            MspOrgId = mspOrgId,
            M365TenantId = request.M365TenantId,
            DisplayName = request.DisplayName,
            Status = TenantStatus.Pending
        };

        _dbContext.ClientTenants.Add(entity);
        await _dbContext.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Created tenant {TenantId} for MSP org {MspOrgId}", entity.Id, mspOrgId);

        return MapToDto(entity);
    }

    public async Task<TenantDto> ProcessConsentCallbackAsync(Guid tenantId, ConsentCallbackRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var tenant = await _dbContext.ClientTenants.FindAsync([tenantId], cancellationToken)
            ?? throw new InvalidOperationException($"Tenant {tenantId} not found.");

        if (!request.AdminConsent || !string.IsNullOrEmpty(request.Error))
        {
            tenant.Status = TenantStatus.Error;
            await _dbContext.SaveChangesAsync(cancellationToken);

            _logger.LogWarning("Consent failed for tenant {TenantId}. Error: {Error} - {Description}",
                tenantId, request.Error, request.ErrorDescription);

            return MapToDto(tenant);
        }

        // Consent succeeded
        tenant.Status = TenantStatus.Connected;
        tenant.ConnectedAt = DateTimeOffset.UtcNow;
        await _dbContext.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Consent granted for tenant {TenantId}. Status updated to Connected.", tenantId);

        return MapToDto(tenant);
    }

    public async Task<IReadOnlyList<TenantDto>> GetTenantsAsync(Guid mspOrgId, CancellationToken cancellationToken = default)
    {
        var tenants = await _dbContext.ClientTenants
            .AsNoTracking()
            .Where(t => t.MspOrgId == mspOrgId)
            .OrderByDescending(t => t.CreatedAt)
            .Select(t => new TenantDto
            {
                Id = t.Id,
                MspOrgId = t.MspOrgId,
                M365TenantId = t.M365TenantId,
                DisplayName = t.DisplayName,
                Status = t.Status,
                ConnectedAt = t.ConnectedAt,
                CreatedAt = t.CreatedAt,
                UpdatedAt = t.UpdatedAt
            })
            .ToListAsync(cancellationToken);

        return tenants;
    }

    public async Task<IReadOnlyList<SharePointSiteDto>> DiscoverSharePointSitesAsync(Guid tenantId, CancellationToken cancellationToken = default)
    {
        var tenant = await _dbContext.ClientTenants
            .FirstOrDefaultAsync(t => t.Id == tenantId, cancellationToken)
            ?? throw new InvalidOperationException($"Tenant {tenantId} not found.");

        if (tenant.Status != TenantStatus.Connected)
            throw new InvalidOperationException($"Tenant {tenantId} is not in Connected status.");

        var discoveredSites = await _siteDiscoveryService.DiscoverSitesAsync(tenant.M365TenantId, cancellationToken);

        // Persist all discovered sites to DB (upsert), preserving existing IsSelected flags
        var existingSites = await _dbContext.SharePointSites
            .Where(s => s.ClientTenantId == tenantId)
            .ToListAsync(cancellationToken);

        var existingLookup = existingSites.ToDictionary(s => s.SiteId);

        foreach (var discovered in discoveredSites)
        {
            if (existingLookup.TryGetValue(discovered.SiteId, out var existing))
            {
                existing.DisplayName = discovered.DisplayName;
                existing.Url = discovered.Url;
                existing.StorageUsedBytes = discovered.StorageUsedBytes;
                discovered.IsSelected = existing.IsSelected;
            }
            else
            {
                _dbContext.SharePointSites.Add(new SharePointSite
                {
                    ClientTenantId = tenantId,
                    MspOrgId = tenant.MspOrgId,
                    SiteId = discovered.SiteId,
                    Url = discovered.Url,
                    DisplayName = discovered.DisplayName,
                    StorageUsedBytes = discovered.StorageUsedBytes,
                    IsSelected = false
                });
            }
        }

        await _dbContext.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Discovered and persisted {SiteCount} sites for tenant {TenantId}",
            discoveredSites.Count, tenantId);

        return discoveredSites;
    }

    public async Task<IReadOnlyList<SharePointSiteDto>> SaveSelectedSitesAsync(Guid tenantId, Guid mspOrgId, SaveSelectedSitesRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        _ = await _dbContext.ClientTenants
            .AsNoTracking()
            .FirstOrDefaultAsync(t => t.Id == tenantId, cancellationToken)
            ?? throw new InvalidOperationException($"Tenant {tenantId} not found.");

        // Update IsSelected flags on already-persisted sites (no redundant Graph API call)
        var existingSites = await _dbContext.SharePointSites
            .Where(s => s.ClientTenantId == tenantId)
            .ToListAsync(cancellationToken);

        var selectedSet = new HashSet<string>(request.SelectedSiteIds);

        foreach (var site in existingSites)
        {
            site.IsSelected = selectedSet.Contains(site.SiteId);
        }

        await _dbContext.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Saved {SelectedCount} selected sites (of {TotalCount} total) for tenant {TenantId}",
            request.SelectedSiteIds.Count, existingSites.Count, tenantId);

        return existingSites
            .Select(s => new SharePointSiteDto
            {
                SiteId = s.SiteId,
                Url = s.Url,
                DisplayName = s.DisplayName,
                StorageUsedBytes = s.StorageUsedBytes,
                IsSelected = s.IsSelected
            })
            .ToList();
    }

    public async Task<IReadOnlyList<TenantSummaryDto>> GetTenantSummariesAsync(Guid mspOrgId, CancellationToken cancellationToken = default)
    {
        var summaries = await _dbContext.ClientTenants
            .AsNoTracking()
            .Where(t => t.MspOrgId == mspOrgId)
            .Select(t => new TenantSummaryDto
            {
                Id = t.Id,
                DisplayName = t.DisplayName,
                Status = t.Status,
                ConnectedAt = t.ConnectedAt,
                SelectedSiteCount = t.SharePointSites.Count(s => s.IsSelected),
                TotalStorageBytes = t.SharePointSites.Where(s => s.IsSelected).Sum(s => s.StorageUsedBytes),
                CreatedAt = t.CreatedAt
            })
            .OrderByDescending(t => t.CreatedAt)
            .ToListAsync(cancellationToken);

        return summaries;
    }

    public async Task<TenantDto> DisconnectTenantAsync(Guid tenantId, Guid mspOrgId, CancellationToken cancellationToken = default)
    {
        var tenant = await _dbContext.ClientTenants
            .FirstOrDefaultAsync(t => t.Id == tenantId && t.MspOrgId == mspOrgId, cancellationToken)
            ?? throw new InvalidOperationException($"Tenant {tenantId} not found.");

        if (tenant.Status == TenantStatus.Disconnected)
            throw new InvalidOperationException($"Tenant {tenantId} is already disconnected.");

        // Mark as Disconnecting
        tenant.Status = TenantStatus.Disconnecting;
        await _dbContext.SaveChangesAsync(cancellationToken);
        _logger.LogInformation("Tenant {TenantId} marked as Disconnecting", tenantId);

        // Delete all SharePoint site records for this tenant
        var sites = await _dbContext.SharePointSites
            .Where(s => s.ClientTenantId == tenantId)
            .ToListAsync(cancellationToken);

        if (sites.Count > 0)
        {
            _dbContext.SharePointSites.RemoveRange(sites);
            _logger.LogInformation("Removed {SiteCount} SharePoint site records for tenant {TenantId}", sites.Count, tenantId);
        }

        // Delete Key Vault secret (OAuth token)
        try
        {
            await _keyVaultService.DeleteSecretAsync($"tenant-{tenantId}-graph-token", cancellationToken);
            _logger.LogInformation("Deleted Key Vault secret for tenant {TenantId}", tenantId);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to delete Key Vault secret for tenant {TenantId}. Secret may not exist.", tenantId);
        }

        // Mark as Disconnected
        tenant.Status = TenantStatus.Disconnected;
        tenant.ConnectedAt = null;
        await _dbContext.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Tenant {TenantId} disconnected successfully. All associated data deleted.", tenantId);

        return MapToDto(tenant);
    }

    private static string ExtractTenantIdFromIssuer(string issuer)
    {
        // issuer format: https://sts.windows.net/{tenant-id}/
        var uri = new Uri(issuer);
        var segments = uri.AbsolutePath.Split('/', StringSplitOptions.RemoveEmptyEntries);
        return segments.Length > 0 ? segments[0] : string.Empty;
    }

    private static TenantDto MapToDto(ClientTenant entity)
    {
        return new TenantDto
        {
            Id = entity.Id,
            MspOrgId = entity.MspOrgId,
            M365TenantId = entity.M365TenantId,
            DisplayName = entity.DisplayName,
            Status = entity.Status,
            ConnectedAt = entity.ConnectedAt,
            CreatedAt = entity.CreatedAt,
            UpdatedAt = entity.UpdatedAt
        };
    }
}
