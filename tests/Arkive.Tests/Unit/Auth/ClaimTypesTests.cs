using Arkive.Core.Constants;

namespace Arkive.Tests.Unit.Auth;

public class ClaimTypesTests
{
    [Fact]
    public void ObjectId_ShouldBeCorrectClaimUri()
    {
        Assert.Equal(
            "http://schemas.microsoft.com/identity/claims/objectidentifier",
            ArkiveClaimTypes.ObjectId);
    }

    [Fact]
    public void TenantId_ShouldBeCorrectClaimUri()
    {
        Assert.Equal(
            "http://schemas.microsoft.com/identity/claims/tenantid",
            ArkiveClaimTypes.TenantId);
    }

    [Fact]
    public void Roles_ShouldBeCorrectClaimUri()
    {
        Assert.Equal(
            "http://schemas.microsoft.com/ws/2008/06/identity/claims/role",
            ArkiveClaimTypes.Roles);
    }

    [Fact]
    public void Name_ShouldBeCorrectValue()
    {
        Assert.Equal("name", ArkiveClaimTypes.Name);
    }

    [Fact]
    public void PreferredUsername_ShouldBeCorrectValue()
    {
        Assert.Equal("preferred_username", ArkiveClaimTypes.PreferredUsername);
    }

    [Fact]
    public void MspOrgId_ShouldBeCorrectValue()
    {
        Assert.Equal("extension_MspOrgId", ArkiveClaimTypes.MspOrgId);
    }
}
