using Arkive.Core.Models;

namespace Arkive.Tests.Unit.Auth;

public class UserProfileTests
{
    [Fact]
    public void DefaultValues_ShouldBeEmpty()
    {
        var profile = new UserProfile();

        Assert.Equal(string.Empty, profile.EntraObjectId);
        Assert.Equal(string.Empty, profile.MspOrgId);
        Assert.Equal(string.Empty, profile.Name);
        Assert.Equal(string.Empty, profile.Email);
        Assert.Equal(string.Empty, profile.Role);
        Assert.Empty(profile.Roles);
    }

    [Fact]
    public void Properties_ShouldBeSettable()
    {
        var profile = new UserProfile
        {
            EntraObjectId = "oid-123",
            MspOrgId = "org-456",
            Name = "Test User",
            Email = "test@example.com",
            Role = "MspAdmin",
            Roles = ["MspAdmin", "MspTech"]
        };

        Assert.Equal("oid-123", profile.EntraObjectId);
        Assert.Equal("org-456", profile.MspOrgId);
        Assert.Equal("Test User", profile.Name);
        Assert.Equal("test@example.com", profile.Email);
        Assert.Equal("MspAdmin", profile.Role);
        Assert.Equal(2, profile.Roles.Count);
    }
}
