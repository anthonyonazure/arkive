using Arkive.Core.Enums;
using Arkive.Core.Models;

namespace Arkive.Tests.Unit.Data;

public class UserEntityTests
{
    [Fact]
    public void DefaultValues_ShouldBeCorrect()
    {
        var user = new User();

        Assert.Equal(Guid.Empty, user.Id);
        Assert.Equal(Guid.Empty, user.MspOrgId);
        Assert.Equal(string.Empty, user.EntraIdObjectId);
        Assert.Equal(string.Empty, user.Email);
        Assert.Equal(string.Empty, user.DisplayName);
        Assert.Equal(UserRole.MspTech, user.Role);
    }

    [Fact]
    public void Properties_ShouldBeSettable()
    {
        var id = Guid.NewGuid();
        var orgId = Guid.NewGuid();
        var now = DateTimeOffset.UtcNow;

        var user = new User
        {
            Id = id,
            MspOrgId = orgId,
            EntraIdObjectId = "oid-abc",
            Email = "user@example.com",
            DisplayName = "Test User",
            Role = UserRole.MspAdmin,
            CreatedAt = now,
            UpdatedAt = now
        };

        Assert.Equal(id, user.Id);
        Assert.Equal(orgId, user.MspOrgId);
        Assert.Equal("oid-abc", user.EntraIdObjectId);
        Assert.Equal("user@example.com", user.Email);
        Assert.Equal("Test User", user.DisplayName);
        Assert.Equal(UserRole.MspAdmin, user.Role);
        Assert.Equal(now, user.CreatedAt);
        Assert.Equal(now, user.UpdatedAt);
    }

    [Fact]
    public void AllRoles_ShouldBeAssignable()
    {
        var user = new User();

        user.Role = UserRole.PlatformAdmin;
        Assert.Equal(UserRole.PlatformAdmin, user.Role);

        user.Role = UserRole.MspAdmin;
        Assert.Equal(UserRole.MspAdmin, user.Role);

        user.Role = UserRole.MspTech;
        Assert.Equal(UserRole.MspTech, user.Role);
    }
}
