using Arkive.Core.Configuration;

namespace Arkive.Tests.Unit.Auth;

public class EntraIdOptionsTests
{
    [Fact]
    public void SectionName_ShouldBeAzureAd()
    {
        Assert.Equal("AzureAd", EntraIdOptions.SectionName);
    }

    [Fact]
    public void DefaultInstance_ShouldBeLoginMicrosoftOnlineCom()
    {
        var options = new EntraIdOptions();
        Assert.Equal("https://login.microsoftonline.com/", options.Instance);
    }

    [Fact]
    public void DefaultValues_ShouldBeEmpty()
    {
        var options = new EntraIdOptions();
        Assert.Equal(string.Empty, options.TenantId);
        Assert.Equal(string.Empty, options.ClientId);
        Assert.Equal(string.Empty, options.Audience);
    }

    [Fact]
    public void Properties_ShouldBeSettable()
    {
        var options = new EntraIdOptions
        {
            TenantId = "test-tenant",
            ClientId = "test-client",
            Audience = "api://test-client",
            Instance = "https://custom.instance/"
        };

        Assert.Equal("test-tenant", options.TenantId);
        Assert.Equal("test-client", options.ClientId);
        Assert.Equal("api://test-client", options.Audience);
        Assert.Equal("https://custom.instance/", options.Instance);
    }
}
