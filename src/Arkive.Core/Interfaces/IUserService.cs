using Arkive.Core.DTOs;

namespace Arkive.Core.Interfaces;

public interface IUserService
{
    Task<UserDto> CreateAsync(CreateUserRequest request, Guid mspOrgId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<UserDto>> GetAllByOrgAsync(Guid mspOrgId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<UserDto>> GetAllAsync(CancellationToken cancellationToken = default);
    Task<UserDto?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<UserDto?> UpdateRoleAsync(Guid id, UpdateUserRequest request, CancellationToken cancellationToken = default);
    Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default);
}
