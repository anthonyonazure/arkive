using Arkive.Core.Configuration;
using Arkive.Core.Interfaces;
using Azure;
using Azure.Security.KeyVault.Secrets;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Arkive.Functions.Services;

public class KeyVaultService : IKeyVaultService
{
    private readonly SecretClient _secretClient;
    private readonly IMemoryCache _cache;
    private readonly ILogger<KeyVaultService> _logger;
    private readonly TimeSpan _cacheTtl;

    public KeyVaultService(
        SecretClient secretClient,
        IMemoryCache cache,
        ILogger<KeyVaultService> logger,
        IOptions<KeyVaultOptions> options)
    {
        _secretClient = secretClient;
        _cache = cache;
        _logger = logger;
        _cacheTtl = TimeSpan.FromMinutes(options.Value.CacheTtlMinutes);
    }

    public async Task<string> GetSecretAsync(string name, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(name);

        var cacheKey = $"kv:{name}";

        if (_cache.TryGetValue(cacheKey, out string? cached) && cached is not null)
        {
            _logger.LogDebug("Key Vault cache hit for secret {SecretName}", name);
            return cached;
        }

        try
        {
            var response = await _secretClient.GetSecretAsync(name, cancellationToken: cancellationToken);
            var value = response.Value.Value;

            _cache.Set(cacheKey, value, new MemoryCacheEntryOptions { SlidingExpiration = _cacheTtl });
            _logger.LogDebug("Retrieved and cached secret {SecretName}", name);

            return value;
        }
        catch (RequestFailedException ex)
        {
            _logger.LogError(ex, "Failed to retrieve secret {SecretName} from Key Vault. Status: {Status}",
                name, ex.Status);
            throw;
        }
    }

    public async Task SetSecretAsync(string name, string value, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(name);
        ArgumentException.ThrowIfNullOrEmpty(value);

        try
        {
            await _secretClient.SetSecretAsync(name, value, cancellationToken);
            _cache.Remove($"kv:{name}");
            _logger.LogInformation("Secret {SecretName} set in Key Vault and cache evicted", name);
        }
        catch (RequestFailedException ex)
        {
            _logger.LogError(ex, "Failed to set secret {SecretName} in Key Vault. Status: {Status}",
                name, ex.Status);
            throw;
        }
    }

    public async Task DeleteSecretAsync(string name, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(name);

        try
        {
            await _secretClient.StartDeleteSecretAsync(name, cancellationToken);
            _cache.Remove($"kv:{name}");
            _logger.LogInformation("Secret {SecretName} deletion started and cache evicted", name);
        }
        catch (RequestFailedException ex)
        {
            _logger.LogError(ex, "Failed to delete secret {SecretName} from Key Vault. Status: {Status}",
                name, ex.Status);
            throw;
        }
    }
}
