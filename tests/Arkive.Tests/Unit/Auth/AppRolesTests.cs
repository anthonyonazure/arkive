using Arkive.Core.Constants;

namespace Arkive.Tests.Unit.Auth;

public class AppRolesTests
{
    [Fact]
    public void PlatformAdmin_ShouldBeCorrectValue()
    {
        Assert.Equal("PlatformAdmin", AppRoles.PlatformAdmin);
    }

    [Fact]
    public void MspAdmin_ShouldBeCorrectValue()
    {
        Assert.Equal("MspAdmin", AppRoles.MspAdmin);
    }

    [Fact]
    public void MspTech_ShouldBeCorrectValue()
    {
        Assert.Equal("MspTech", AppRoles.MspTech);
    }
}
