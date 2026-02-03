using Arkive.Core.Configuration;

namespace Arkive.Tests.Unit.Configuration;

public class KeyVaultOptionsTests
{
    [Fact]
    public void SectionName_ShouldBeKeyVault()
    {
        Assert.Equal("KeyVault", KeyVaultOptions.SectionName);
    }

    [Fact]
    public void DefaultValues_ShouldBeCorrect()
    {
        var options = new KeyVaultOptions();

        Assert.Equal(string.Empty, options.VaultUri);
        Assert.Equal(15, options.CacheTtlMinutes);
    }

    [Fact]
    public void Properties_ShouldBeSettable()
    {
        var options = new KeyVaultOptions
        {
            VaultUri = "https://my-vault.vault.azure.net/",
            CacheTtlMinutes = 30
        };

        Assert.Equal("https://my-vault.vault.azure.net/", options.VaultUri);
        Assert.Equal(30, options.CacheTtlMinutes);
    }
}
