using Arkive.Data;
using Arkive.Data.Extensions;

namespace Arkive.Tests.Unit.Data;

public class TenantContextTests
{
    [Fact]
    public void DefaultValues_ShouldBeEmpty()
    {
        var context = new TenantContext();

        Assert.Equal(string.Empty, context.MspOrgId);
        Assert.Null(context.ClientTenantId);
    }

    [Fact]
    public void Properties_ShouldBeSettable()
    {
        var context = new TenantContext
        {
            MspOrgId = "org-123",
            ClientTenantId = "tenant-456"
        };

        Assert.Equal("org-123", context.MspOrgId);
        Assert.Equal("tenant-456", context.ClientTenantId);
    }

    [Fact]
    public void SetFromClaims_ShouldSetBothValues()
    {
        var context = new TenantContext();

        context.SetFromClaims("org-abc", "tenant-xyz");

        Assert.Equal("org-abc", context.MspOrgId);
        Assert.Equal("tenant-xyz", context.ClientTenantId);
    }

    [Fact]
    public void SetFromClaims_WithoutClientTenantId_ShouldSetMspOrgIdOnly()
    {
        var context = new TenantContext();

        context.SetFromClaims("org-abc");

        Assert.Equal("org-abc", context.MspOrgId);
        Assert.Null(context.ClientTenantId);
    }
}
