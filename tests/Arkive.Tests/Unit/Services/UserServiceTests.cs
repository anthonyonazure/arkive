using Arkive.Core.DTOs;
using Arkive.Core.Enums;
using Arkive.Core.Models;
using Arkive.Data;
using Arkive.Functions.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace Arkive.Tests.Unit.Services;

public class UserServiceTests : IDisposable
{
    private readonly ArkiveDbContext _dbContext;
    private readonly UserService _service;
    private readonly Guid _orgId = Guid.NewGuid();

    public UserServiceTests()
    {
        var options = new DbContextOptionsBuilder<ArkiveDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        _dbContext = new ArkiveDbContext(options);
        var logger = NullLogger<UserService>.Instance;
        _service = new UserService(_dbContext, logger);

        // Seed an MspOrganization for FK relationship
        _dbContext.MspOrganizations.Add(new MspOrganization
        {
            Id = _orgId,
            Name = "Test Org",
            EntraIdTenantId = "00000000-0000-0000-0000-000000000001",
            SubscriptionTier = SubscriptionTier.Starter
        });
        _dbContext.SaveChanges();
    }

    public void Dispose()
    {
        _dbContext.Dispose();
    }

    [Fact]
    public async Task CreateAsync_CreatesUserWithCorrectMspOrgIdAndRole()
    {
        var request = new CreateUserRequest
        {
            EntraIdObjectId = "entra-001",
            Email = "user@test.com",
            DisplayName = "Test User",
            Role = UserRole.MspTech
        };

        var result = await _service.CreateAsync(request, _orgId);

        Assert.Equal("user@test.com", result.Email);
        Assert.Equal("Test User", result.DisplayName);
        Assert.Equal(UserRole.MspTech, result.Role);
        Assert.Equal(_orgId, result.MspOrgId);
        Assert.Equal("entra-001", result.EntraIdObjectId);
    }

    [Fact]
    public async Task CreateAsync_PersistsToDatabase()
    {
        var request = new CreateUserRequest
        {
            EntraIdObjectId = "entra-002",
            Email = "persist@test.com",
            DisplayName = "Persist User",
            Role = UserRole.MspAdmin
        };

        var result = await _service.CreateAsync(request, _orgId);

        var entity = await _dbContext.Users.FindAsync(result.Id);
        Assert.NotNull(entity);
        Assert.Equal("persist@test.com", entity.Email);
        Assert.Equal(UserRole.MspAdmin, entity.Role);
        Assert.Equal(_orgId, entity.MspOrgId);
    }

    [Fact]
    public async Task GetAllByOrgAsync_ReturnsOnlyUsersForGivenOrg()
    {
        var otherOrgId = Guid.NewGuid();
        _dbContext.MspOrganizations.Add(new MspOrganization
        {
            Id = otherOrgId,
            Name = "Other Org",
            EntraIdTenantId = "00000000-0000-0000-0000-000000000002",
            SubscriptionTier = SubscriptionTier.Starter
        });
        _dbContext.SaveChanges();

        await _service.CreateAsync(new CreateUserRequest
        {
            EntraIdObjectId = "entra-010",
            Email = "org1@test.com",
            DisplayName = "Org1 User",
            Role = UserRole.MspTech
        }, _orgId);

        await _service.CreateAsync(new CreateUserRequest
        {
            EntraIdObjectId = "entra-011",
            Email = "org2@test.com",
            DisplayName = "Org2 User",
            Role = UserRole.MspTech
        }, otherOrgId);

        var results = await _service.GetAllByOrgAsync(_orgId);

        Assert.Single(results);
        Assert.Equal("org1@test.com", results[0].Email);
    }

    [Fact]
    public async Task GetAllAsync_ReturnsAllUsersAcrossOrgs()
    {
        var otherOrgId = Guid.NewGuid();
        _dbContext.MspOrganizations.Add(new MspOrganization
        {
            Id = otherOrgId,
            Name = "Other Org 2",
            EntraIdTenantId = "00000000-0000-0000-0000-000000000003",
            SubscriptionTier = SubscriptionTier.Starter
        });
        _dbContext.SaveChanges();

        await _service.CreateAsync(new CreateUserRequest
        {
            EntraIdObjectId = "entra-020",
            Email = "all1@test.com",
            DisplayName = "User1",
            Role = UserRole.MspTech
        }, _orgId);

        await _service.CreateAsync(new CreateUserRequest
        {
            EntraIdObjectId = "entra-021",
            Email = "all2@test.com",
            DisplayName = "User2",
            Role = UserRole.MspAdmin
        }, otherOrgId);

        var results = await _service.GetAllAsync();

        Assert.Equal(2, results.Count);
    }

    [Fact]
    public async Task GetByIdAsync_ReturnsUserWhenExists()
    {
        var created = await _service.CreateAsync(new CreateUserRequest
        {
            EntraIdObjectId = "entra-030",
            Email = "findme@test.com",
            DisplayName = "Find Me",
            Role = UserRole.MspTech
        }, _orgId);

        var result = await _service.GetByIdAsync(created.Id);

        Assert.NotNull(result);
        Assert.Equal("findme@test.com", result.Email);
        Assert.Equal(created.Id, result.Id);
    }

    [Fact]
    public async Task GetByIdAsync_ReturnsNullWhenNotFound()
    {
        var result = await _service.GetByIdAsync(Guid.NewGuid());

        Assert.Null(result);
    }

    [Fact]
    public async Task UpdateRoleAsync_UpdatesRoleSuccessfully()
    {
        var created = await _service.CreateAsync(new CreateUserRequest
        {
            EntraIdObjectId = "entra-040",
            Email = "update@test.com",
            DisplayName = "Update User",
            Role = UserRole.MspTech
        }, _orgId);

        var result = await _service.UpdateRoleAsync(created.Id, new UpdateUserRequest { Role = UserRole.MspAdmin });

        Assert.NotNull(result);
        Assert.Equal(UserRole.MspAdmin, result.Role);
        Assert.Equal(created.Id, result.Id);
    }

    [Fact]
    public async Task UpdateRoleAsync_ReturnsNullWhenNotFound()
    {
        var result = await _service.UpdateRoleAsync(Guid.NewGuid(), new UpdateUserRequest { Role = UserRole.MspAdmin });

        Assert.Null(result);
    }

    [Fact]
    public async Task DeleteAsync_RemovesUserAndReturnsTrue()
    {
        var created = await _service.CreateAsync(new CreateUserRequest
        {
            EntraIdObjectId = "entra-050",
            Email = "delete@test.com",
            DisplayName = "Delete User",
            Role = UserRole.MspTech
        }, _orgId);

        var result = await _service.DeleteAsync(created.Id);

        Assert.True(result);
        var entity = await _dbContext.Users.FindAsync(created.Id);
        Assert.Null(entity);
    }

    [Fact]
    public async Task DeleteAsync_ReturnsFalseWhenNotFound()
    {
        var result = await _service.DeleteAsync(Guid.NewGuid());

        Assert.False(result);
    }

    [Fact]
    public async Task CreateAsync_WithNullRequest_ThrowsArgumentNullException()
    {
        await Assert.ThrowsAsync<ArgumentNullException>(() => _service.CreateAsync(null!, _orgId));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public async Task CreateAsync_WithNullOrEmptyEntraIdObjectId_ThrowsArgumentException(string? entraId)
    {
        var request = new CreateUserRequest
        {
            EntraIdObjectId = entraId!,
            Email = "valid@test.com",
            DisplayName = "Valid",
            Role = UserRole.MspTech
        };

        await Assert.ThrowsAnyAsync<ArgumentException>(() => _service.CreateAsync(request, _orgId));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public async Task CreateAsync_WithNullOrEmptyEmail_ThrowsArgumentException(string? email)
    {
        var request = new CreateUserRequest
        {
            EntraIdObjectId = "entra-valid",
            Email = email!,
            DisplayName = "Valid",
            Role = UserRole.MspTech
        };

        await Assert.ThrowsAnyAsync<ArgumentException>(() => _service.CreateAsync(request, _orgId));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public async Task CreateAsync_WithNullOrEmptyDisplayName_ThrowsArgumentException(string? displayName)
    {
        var request = new CreateUserRequest
        {
            EntraIdObjectId = "entra-valid",
            Email = "valid@test.com",
            DisplayName = displayName!,
            Role = UserRole.MspTech
        };

        await Assert.ThrowsAnyAsync<ArgumentException>(() => _service.CreateAsync(request, _orgId));
    }

    [Fact]
    public async Task UpdateRoleAsync_WithNullRequest_ThrowsArgumentNullException()
    {
        var created = await _service.CreateAsync(new CreateUserRequest
        {
            EntraIdObjectId = "entra-060",
            Email = "nullreq@test.com",
            DisplayName = "Null Req User",
            Role = UserRole.MspTech
        }, _orgId);

        await Assert.ThrowsAsync<ArgumentNullException>(() => _service.UpdateRoleAsync(created.Id, null!));
    }
}
