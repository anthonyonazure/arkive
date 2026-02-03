using Arkive.Core.DTOs;

namespace Arkive.Core.Interfaces;

public interface ISiteDiscoveryService
{
    Task<IReadOnlyList<SharePointSiteDto>> DiscoverSitesAsync(string m365TenantId, CancellationToken cancellationToken = default);
}
