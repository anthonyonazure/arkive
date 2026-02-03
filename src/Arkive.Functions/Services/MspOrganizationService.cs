using Arkive.Core.DTOs;
using Arkive.Core.Enums;
using Arkive.Core.Interfaces;
using Arkive.Core.Models;
using Arkive.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Arkive.Functions.Services;

public class MspOrganizationService : IMspOrganizationService
{
    private readonly ArkiveDbContext _dbContext;
    private readonly ILogger<MspOrganizationService> _logger;

    public MspOrganizationService(ArkiveDbContext dbContext, ILogger<MspOrganizationService> logger)
    {
        _dbContext = dbContext;
        _logger = logger;
    }

    public async Task<MspOrganizationDto> CreateAsync(CreateMspOrganizationRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentException.ThrowIfNullOrEmpty(request.Name);
        ArgumentException.ThrowIfNullOrEmpty(request.EntraIdTenantId);

        var entity = new MspOrganization
        {
            Name = request.Name,
            EntraIdTenantId = request.EntraIdTenantId,
            SubscriptionTier = SubscriptionTier.Starter
        };

        _dbContext.MspOrganizations.Add(entity);
        await _dbContext.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Created MSP organization {OrgId} with name {OrgName}", entity.Id, entity.Name);

        return MapToDto(entity, 0, 0);
    }

    public async Task<IReadOnlyList<MspOrganizationDto>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        var orgs = await _dbContext.MspOrganizations
            .AsNoTracking()
            .Select(o => new MspOrganizationDto
            {
                Id = o.Id,
                Name = o.Name,
                SubscriptionTier = o.SubscriptionTier,
                EntraIdTenantId = o.EntraIdTenantId,
                CreatedAt = o.CreatedAt,
                UpdatedAt = o.UpdatedAt,
                UserCount = o.Users.Count,
                TenantCount = o.ClientTenants.Count
            })
            .ToListAsync(cancellationToken);

        _logger.LogDebug("Retrieved {OrgCount} MSP organizations", orgs.Count);

        return orgs;
    }

    public async Task<MspOrganizationDto?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var dto = await _dbContext.MspOrganizations
            .AsNoTracking()
            .Where(o => o.Id == id)
            .Select(o => new MspOrganizationDto
            {
                Id = o.Id,
                Name = o.Name,
                SubscriptionTier = o.SubscriptionTier,
                EntraIdTenantId = o.EntraIdTenantId,
                CreatedAt = o.CreatedAt,
                UpdatedAt = o.UpdatedAt,
                UserCount = o.Users.Count,
                TenantCount = o.ClientTenants.Count
            })
            .FirstOrDefaultAsync(cancellationToken);

        if (dto is null)
            _logger.LogDebug("MSP organization {OrgId} not found", id);

        return dto;
    }

    private static MspOrganizationDto MapToDto(MspOrganization entity, int userCount, int tenantCount)
    {
        return new MspOrganizationDto
        {
            Id = entity.Id,
            Name = entity.Name,
            SubscriptionTier = entity.SubscriptionTier,
            EntraIdTenantId = entity.EntraIdTenantId,
            CreatedAt = entity.CreatedAt,
            UpdatedAt = entity.UpdatedAt,
            UserCount = userCount,
            TenantCount = tenantCount
        };
    }
}
