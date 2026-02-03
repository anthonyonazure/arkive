using Arkive.Core.Enums;
using Arkive.Core.Models;

namespace Arkive.Tests.Unit.Data;

public class ClientTenantTests
{
    [Fact]
    public void DefaultValues_ShouldBeCorrect()
    {
        var tenant = new ClientTenant();

        Assert.Equal(Guid.Empty, tenant.Id);
        Assert.Equal(Guid.Empty, tenant.MspOrgId);
        Assert.Equal(string.Empty, tenant.M365TenantId);
        Assert.Equal(string.Empty, tenant.DisplayName);
        Assert.Equal(TenantStatus.Pending, tenant.Status);
        Assert.False(tenant.ReviewFlagged);
        Assert.Equal(7, tenant.AutoApprovalDays);
    }

    [Fact]
    public void Properties_ShouldBeSettable()
    {
        var id = Guid.NewGuid();
        var orgId = Guid.NewGuid();
        var now = DateTimeOffset.UtcNow;

        var tenant = new ClientTenant
        {
            Id = id,
            MspOrgId = orgId,
            M365TenantId = "m365-tenant-xyz",
            DisplayName = "Contoso Ltd",
            Status = TenantStatus.Connected,
            ReviewFlagged = true,
            AutoApprovalDays = 14,
            CreatedAt = now,
            UpdatedAt = now
        };

        Assert.Equal(id, tenant.Id);
        Assert.Equal(orgId, tenant.MspOrgId);
        Assert.Equal("m365-tenant-xyz", tenant.M365TenantId);
        Assert.Equal("Contoso Ltd", tenant.DisplayName);
        Assert.Equal(TenantStatus.Connected, tenant.Status);
        Assert.True(tenant.ReviewFlagged);
        Assert.Equal(14, tenant.AutoApprovalDays);
        Assert.Equal(now, tenant.CreatedAt);
        Assert.Equal(now, tenant.UpdatedAt);
    }

    [Fact]
    public void AutoApprovalDays_ShouldAcceptNullForDisabled()
    {
        var tenant = new ClientTenant { AutoApprovalDays = null };
        Assert.Null(tenant.AutoApprovalDays);
    }

    [Fact]
    public void AutoApprovalDays_ShouldAcceptZeroForImmediate()
    {
        var tenant = new ClientTenant { AutoApprovalDays = 0 };
        Assert.Equal(0, tenant.AutoApprovalDays);
    }

    [Fact]
    public void AllStatuses_ShouldBeAssignable()
    {
        var tenant = new ClientTenant();

        foreach (var status in Enum.GetValues<TenantStatus>())
        {
            tenant.Status = status;
            Assert.Equal(status, tenant.Status);
        }
    }
}
