using Arkive.Core.DTOs;
using Arkive.Core.Enums;

namespace Arkive.Tests.Unit.DTOs;

public class MspOrganizationDtoTests
{
    [Fact]
    public void DefaultValues_ShouldBeCorrect()
    {
        var dto = new MspOrganizationDto();

        Assert.Equal(Guid.Empty, dto.Id);
        Assert.Equal(string.Empty, dto.Name);
        Assert.Equal(SubscriptionTier.Free, dto.SubscriptionTier);
        Assert.Equal(string.Empty, dto.EntraIdTenantId);
        Assert.Equal(default, dto.CreatedAt);
        Assert.Equal(default, dto.UpdatedAt);
        Assert.Equal(0, dto.UserCount);
        Assert.Equal(0, dto.TenantCount);
    }

    [Fact]
    public void Properties_ShouldBeSettable()
    {
        var id = Guid.NewGuid();
        var now = DateTimeOffset.UtcNow;

        var dto = new MspOrganizationDto
        {
            Id = id,
            Name = "Contoso MSP",
            SubscriptionTier = SubscriptionTier.Professional,
            EntraIdTenantId = "abc-123-def-456",
            CreatedAt = now,
            UpdatedAt = now,
            UserCount = 5,
            TenantCount = 10
        };

        Assert.Equal(id, dto.Id);
        Assert.Equal("Contoso MSP", dto.Name);
        Assert.Equal(SubscriptionTier.Professional, dto.SubscriptionTier);
        Assert.Equal("abc-123-def-456", dto.EntraIdTenantId);
        Assert.Equal(now, dto.CreatedAt);
        Assert.Equal(now, dto.UpdatedAt);
        Assert.Equal(5, dto.UserCount);
        Assert.Equal(10, dto.TenantCount);
    }

    [Fact]
    public void CreateMspOrganizationRequest_DefaultValues_ShouldBeCorrect()
    {
        var request = new CreateMspOrganizationRequest();

        Assert.Equal(string.Empty, request.Name);
        Assert.Equal(string.Empty, request.EntraIdTenantId);
    }

    [Fact]
    public void CreateMspOrganizationRequest_Properties_ShouldBeSettable()
    {
        var request = new CreateMspOrganizationRequest
        {
            Name = "Test Org",
            EntraIdTenantId = "tenant-id-123"
        };

        Assert.Equal("Test Org", request.Name);
        Assert.Equal("tenant-id-123", request.EntraIdTenantId);
    }
}
