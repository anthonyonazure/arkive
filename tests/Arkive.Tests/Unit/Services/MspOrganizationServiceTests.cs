using Arkive.Core.DTOs;
using Arkive.Core.Enums;
using Arkive.Data;
using Arkive.Functions.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace Arkive.Tests.Unit.Services;

public class MspOrganizationServiceTests : IDisposable
{
    private readonly ArkiveDbContext _dbContext;
    private readonly MspOrganizationService _service;

    public MspOrganizationServiceTests()
    {
        var options = new DbContextOptionsBuilder<ArkiveDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        _dbContext = new ArkiveDbContext(options);
        var logger = NullLogger<MspOrganizationService>.Instance;
        _service = new MspOrganizationService(_dbContext, logger);
    }

    public void Dispose()
    {
        _dbContext.Dispose();
    }

    [Fact]
    public async Task CreateAsync_CreatesOrgWithStarterTier()
    {
        var request = new CreateMspOrganizationRequest
        {
            Name = "Test MSP",
            EntraIdTenantId = "00000000-0000-0000-0000-000000000001"
        };

        var result = await _service.CreateAsync(request);

        Assert.Equal("Test MSP", result.Name);
        Assert.Equal(SubscriptionTier.Starter, result.SubscriptionTier);
        Assert.Equal("00000000-0000-0000-0000-000000000001", result.EntraIdTenantId);
        Assert.Equal(0, result.UserCount);
        Assert.Equal(0, result.TenantCount);
    }

    [Fact]
    public async Task CreateAsync_PersistsToDatabase()
    {
        var request = new CreateMspOrganizationRequest
        {
            Name = "Persisted Org",
            EntraIdTenantId = "00000000-0000-0000-0000-000000000002"
        };

        var result = await _service.CreateAsync(request);

        var entity = await _dbContext.MspOrganizations.FindAsync(result.Id);
        Assert.NotNull(entity);
        Assert.Equal("Persisted Org", entity.Name);
        Assert.Equal(SubscriptionTier.Starter, entity.SubscriptionTier);
    }

    [Fact]
    public async Task GetAllAsync_ReturnsAllOrganizations()
    {
        var request1 = new CreateMspOrganizationRequest
        {
            Name = "Org One",
            EntraIdTenantId = "00000000-0000-0000-0000-000000000003"
        };
        var request2 = new CreateMspOrganizationRequest
        {
            Name = "Org Two",
            EntraIdTenantId = "00000000-0000-0000-0000-000000000004"
        };

        await _service.CreateAsync(request1);
        await _service.CreateAsync(request2);

        var results = await _service.GetAllAsync();

        Assert.Equal(2, results.Count);
        Assert.Contains(results, o => o.Name == "Org One");
        Assert.Contains(results, o => o.Name == "Org Two");
    }

    [Fact]
    public async Task GetAllAsync_ReturnsEmptyListWhenNoOrgs()
    {
        var results = await _service.GetAllAsync();

        Assert.Empty(results);
    }

    [Fact]
    public async Task GetByIdAsync_ReturnsOrgWhenExists()
    {
        var request = new CreateMspOrganizationRequest
        {
            Name = "Find Me",
            EntraIdTenantId = "00000000-0000-0000-0000-000000000005"
        };
        var created = await _service.CreateAsync(request);

        var result = await _service.GetByIdAsync(created.Id);

        Assert.NotNull(result);
        Assert.Equal("Find Me", result.Name);
        Assert.Equal(created.Id, result.Id);
    }

    [Fact]
    public async Task GetByIdAsync_ReturnsNullWhenNotFound()
    {
        var result = await _service.GetByIdAsync(Guid.NewGuid());

        Assert.Null(result);
    }

    [Fact]
    public async Task CreateAsync_WithNullRequest_ThrowsArgumentNullException()
    {
        await Assert.ThrowsAsync<ArgumentNullException>(() => _service.CreateAsync(null!));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public async Task CreateAsync_WithNullOrEmptyName_ThrowsArgumentException(string? name)
    {
        var request = new CreateMspOrganizationRequest
        {
            Name = name!,
            EntraIdTenantId = "00000000-0000-0000-0000-000000000006"
        };

        await Assert.ThrowsAnyAsync<ArgumentException>(() => _service.CreateAsync(request));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public async Task CreateAsync_WithNullOrEmptyEntraIdTenantId_ThrowsArgumentException(string? tenantId)
    {
        var request = new CreateMspOrganizationRequest
        {
            Name = "Valid Name",
            EntraIdTenantId = tenantId!
        };

        await Assert.ThrowsAnyAsync<ArgumentException>(() => _service.CreateAsync(request));
    }
}
