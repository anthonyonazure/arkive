using Arkive.Core.DTOs;

namespace Arkive.Core.Interfaces;

public interface ITenantOnboardingService
{
    Task<ValidateDomainResponse> ValidateTenantDomainAsync(string domain, CancellationToken cancellationToken = default);
    Task<TenantDto> CreateTenantAsync(Guid mspOrgId, CreateTenantRequest request, CancellationToken cancellationToken = default);
    Task<TenantDto> ProcessConsentCallbackAsync(Guid tenantId, ConsentCallbackRequest request, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<TenantDto>> GetTenantsAsync(Guid mspOrgId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<SharePointSiteDto>> DiscoverSharePointSitesAsync(Guid tenantId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<SharePointSiteDto>> SaveSelectedSitesAsync(Guid tenantId, Guid mspOrgId, SaveSelectedSitesRequest request, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<TenantSummaryDto>> GetTenantSummariesAsync(Guid mspOrgId, CancellationToken cancellationToken = default);
    Task<TenantDto> DisconnectTenantAsync(Guid tenantId, Guid mspOrgId, CancellationToken cancellationToken = default);
}
