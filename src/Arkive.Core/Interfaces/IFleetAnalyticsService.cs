using Arkive.Core.DTOs;

namespace Arkive.Core.Interfaces;

public interface IFleetAnalyticsService
{
    Task<FleetOverviewDto> GetFleetOverviewAsync(Guid mspOrgId, CancellationToken cancellationToken = default);
    Task<TenantAnalyticsDto> GetTenantAnalyticsAsync(Guid tenantId, Guid mspOrgId, CancellationToken cancellationToken = default);
    Task<SiteFilesDto> GetSiteFilesAsync(Guid tenantId, string siteId, Guid mspOrgId, int page = 1, int pageSize = 50, string? sortBy = null, string? sortDir = null, int? minAgeDays = null, string? fileType = null, long? minSizeBytes = null, long? maxSizeBytes = null, CancellationToken cancellationToken = default);
}
