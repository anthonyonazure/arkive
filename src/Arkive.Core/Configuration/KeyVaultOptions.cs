namespace Arkive.Core.Configuration;

public class KeyVaultOptions
{
    public const string SectionName = "KeyVault";

    public string VaultUri { get; set; } = string.Empty;
    public int CacheTtlMinutes { get; set; } = 15;
}
