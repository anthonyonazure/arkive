using Arkive.Core.DTOs;

namespace Arkive.Core.Interfaces;

public interface IMspOrganizationService
{
    Task<MspOrganizationDto> CreateAsync(CreateMspOrganizationRequest request, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<MspOrganizationDto>> GetAllAsync(CancellationToken cancellationToken = default);
    Task<MspOrganizationDto?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
}
