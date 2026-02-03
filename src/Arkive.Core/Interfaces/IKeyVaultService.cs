namespace Arkive.Core.Interfaces;

/// <summary>
/// Abstracts Azure Key Vault secret operations.
/// Implementations handle caching, authentication, and error handling.
/// </summary>
public interface IKeyVaultService
{
    /// <summary>
    /// Retrieves a secret value by name. Returns cached value if available.
    /// </summary>
    Task<string> GetSecretAsync(string name, CancellationToken cancellationToken = default);

    /// <summary>
    /// Creates or updates a secret in Key Vault. Evicts cached value.
    /// </summary>
    Task SetSecretAsync(string name, string value, CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes a secret from Key Vault. Evicts cached value.
    /// </summary>
    Task DeleteSecretAsync(string name, CancellationToken cancellationToken = default);
}
