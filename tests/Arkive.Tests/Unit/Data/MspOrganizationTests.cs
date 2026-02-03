using Arkive.Core.Enums;
using Arkive.Core.Models;

namespace Arkive.Tests.Unit.Data;

public class MspOrganizationTests
{
    [Fact]
    public void DefaultValues_ShouldBeCorrect()
    {
        var org = new MspOrganization();

        Assert.Equal(Guid.Empty, org.Id);
        Assert.Equal(string.Empty, org.Name);
        Assert.Equal(SubscriptionTier.Free, org.SubscriptionTier);
        Assert.Equal(string.Empty, org.EntraIdTenantId);
        Assert.Empty(org.Users);
        Assert.Empty(org.ClientTenants);
    }

    [Fact]
    public void Properties_ShouldBeSettable()
    {
        var id = Guid.NewGuid();
        var now = DateTimeOffset.UtcNow;

        var org = new MspOrganization
        {
            Id = id,
            Name = "Test MSP",
            SubscriptionTier = SubscriptionTier.Professional,
            EntraIdTenantId = "tenant-123",
            CreatedAt = now,
            UpdatedAt = now
        };

        Assert.Equal(id, org.Id);
        Assert.Equal("Test MSP", org.Name);
        Assert.Equal(SubscriptionTier.Professional, org.SubscriptionTier);
        Assert.Equal("tenant-123", org.EntraIdTenantId);
        Assert.Equal(now, org.CreatedAt);
        Assert.Equal(now, org.UpdatedAt);
    }

    [Fact]
    public void NavigationProperties_ShouldBeInitializedEmpty()
    {
        var org = new MspOrganization();

        Assert.NotNull(org.Users);
        Assert.NotNull(org.ClientTenants);
        Assert.Empty(org.Users);
        Assert.Empty(org.ClientTenants);
    }
}
