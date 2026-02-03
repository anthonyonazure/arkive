using System.Linq.Expressions;
using Arkive.Core.DTOs;
using Arkive.Core.Interfaces;
using Arkive.Core.Models;
using Arkive.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Arkive.Functions.Services;

public class UserService : IUserService
{
    private readonly ArkiveDbContext _dbContext;
    private readonly ILogger<UserService> _logger;

    private static readonly Expression<Func<User, UserDto>> ProjectToDto = u => new UserDto
    {
        Id = u.Id,
        MspOrgId = u.MspOrgId,
        EntraIdObjectId = u.EntraIdObjectId,
        Email = u.Email,
        DisplayName = u.DisplayName,
        Role = u.Role,
        CreatedAt = u.CreatedAt,
        UpdatedAt = u.UpdatedAt
    };

    public UserService(ArkiveDbContext dbContext, ILogger<UserService> logger)
    {
        _dbContext = dbContext;
        _logger = logger;
    }

    public async Task<UserDto> CreateAsync(CreateUserRequest request, Guid mspOrgId, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentException.ThrowIfNullOrEmpty(request.EntraIdObjectId);
        ArgumentException.ThrowIfNullOrEmpty(request.Email);
        ArgumentException.ThrowIfNullOrEmpty(request.DisplayName);

        var entity = new User
        {
            MspOrgId = mspOrgId,
            EntraIdObjectId = request.EntraIdObjectId,
            Email = request.Email,
            DisplayName = request.DisplayName,
            Role = request.Role
        };

        _dbContext.Users.Add(entity);
        await _dbContext.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Created user {UserId} with email {UserEmail} in org {OrgId}", entity.Id, entity.Email, mspOrgId);

        return MapToDto(entity);
    }

    public async Task<IReadOnlyList<UserDto>> GetAllByOrgAsync(Guid mspOrgId, CancellationToken cancellationToken = default)
    {
        var users = await _dbContext.Users
            .AsNoTracking()
            .Where(u => u.MspOrgId == mspOrgId)
            .Select(ProjectToDto)
            .ToListAsync(cancellationToken);

        _logger.LogDebug("Retrieved {UserCount} users for org {OrgId}", users.Count, mspOrgId);

        return users;
    }

    public async Task<IReadOnlyList<UserDto>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        var users = await _dbContext.Users
            .AsNoTracking()
            .Select(ProjectToDto)
            .ToListAsync(cancellationToken);

        _logger.LogDebug("Retrieved {UserCount} users (all orgs)", users.Count);

        return users;
    }

    public async Task<UserDto?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var dto = await _dbContext.Users
            .AsNoTracking()
            .Where(u => u.Id == id)
            .Select(ProjectToDto)
            .FirstOrDefaultAsync(cancellationToken);

        if (dto is null)
            _logger.LogDebug("User {UserId} not found", id);

        return dto;
    }

    public async Task<UserDto?> UpdateRoleAsync(Guid id, UpdateUserRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (!Enum.IsDefined(request.Role))
            throw new ArgumentException("Invalid role specified.", nameof(request));

        var entity = await _dbContext.Users.FindAsync(new object[] { id }, cancellationToken);
        if (entity is null)
        {
            _logger.LogDebug("User {UserId} not found for role update", id);
            return null;
        }

        entity.Role = request.Role;
        await _dbContext.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Updated user {UserId} role to {UserRole}", id, request.Role);

        return MapToDto(entity);
    }

    public async Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var entity = await _dbContext.Users.FindAsync(new object[] { id }, cancellationToken);
        if (entity is null)
        {
            _logger.LogDebug("User {UserId} not found for deletion", id);
            return false;
        }

        _dbContext.Users.Remove(entity);
        await _dbContext.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Deleted user {UserId} from org {OrgId}", id, entity.MspOrgId);

        return true;
    }

    private static UserDto MapToDto(User entity)
    {
        return new UserDto
        {
            Id = entity.Id,
            MspOrgId = entity.MspOrgId,
            EntraIdObjectId = entity.EntraIdObjectId,
            Email = entity.Email,
            DisplayName = entity.DisplayName,
            Role = entity.Role,
            CreatedAt = entity.CreatedAt,
            UpdatedAt = entity.UpdatedAt
        };
    }
}
