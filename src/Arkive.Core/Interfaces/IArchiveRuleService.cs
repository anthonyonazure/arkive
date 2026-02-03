using Arkive.Core.DTOs;

namespace Arkive.Core.Interfaces;

public interface IArchiveRuleService
{
    Task<ArchiveRuleDto> CreateAsync(Guid tenantId, Guid mspOrgId, CreateArchiveRuleRequest request, string? createdBy = null, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<ArchiveRuleDto>> GetAllByTenantAsync(Guid tenantId, Guid mspOrgId, string? ruleType = null, CancellationToken cancellationToken = default);
    Task<ArchiveRuleDto?> GetByIdAsync(Guid tenantId, Guid ruleId, Guid mspOrgId, CancellationToken cancellationToken = default);
    Task<ArchiveRuleDto?> UpdateAsync(Guid tenantId, Guid ruleId, Guid mspOrgId, UpdateArchiveRuleRequest request, CancellationToken cancellationToken = default);
    Task<bool> DeleteAsync(Guid tenantId, Guid ruleId, Guid mspOrgId, CancellationToken cancellationToken = default);
}
