using Arkive.Core.Configuration;
using Arkive.Functions.Services;
using Azure;
using Azure.Security.KeyVault.Secrets;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NSubstitute;

namespace Arkive.Tests.Unit.Services;

public class KeyVaultServiceTests : IDisposable
{
    private readonly SecretClient _secretClient;
    private readonly MemoryCache _cache;
    private readonly KeyVaultService _service;

    public KeyVaultServiceTests()
    {
        _secretClient = Substitute.For<SecretClient>();
        _cache = new MemoryCache(new MemoryCacheOptions());

        var options = Options.Create(new KeyVaultOptions { CacheTtlMinutes = 15 });
        var logger = NullLogger<KeyVaultService>.Instance;

        _service = new KeyVaultService(_secretClient, _cache, logger, options);
    }

    public void Dispose()
    {
        _cache.Dispose();
    }

    [Fact]
    public async Task GetSecretAsync_ReturnsCachedValueOnSecondCall()
    {
        // Arrange
        var secret = new KeyVaultSecret("test-secret", "secret-value");
        _secretClient.GetSecretAsync("test-secret", cancellationToken: Arg.Any<CancellationToken>())
            .Returns(Response.FromValue(secret, Substitute.For<Response>()));

        // Act
        var first = await _service.GetSecretAsync("test-secret");
        var second = await _service.GetSecretAsync("test-secret");

        // Assert
        Assert.Equal("secret-value", first);
        Assert.Equal("secret-value", second);

        // SecretClient should only be called once (second call hits cache)
        await _secretClient.Received(1)
            .GetSecretAsync("test-secret", cancellationToken: Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task SetSecretAsync_EvictsCache()
    {
        // Arrange
        var secret = new KeyVaultSecret("test-secret", "original-value");
        _secretClient.GetSecretAsync("test-secret", cancellationToken: Arg.Any<CancellationToken>())
            .Returns(Response.FromValue(secret, Substitute.For<Response>()));

        // Populate cache
        await _service.GetSecretAsync("test-secret");

        // Reconfigure mock for second fetch (after eviction)
        var updatedSecret = new KeyVaultSecret("test-secret", "updated-value");
        _secretClient.GetSecretAsync("test-secret", cancellationToken: Arg.Any<CancellationToken>())
            .Returns(Response.FromValue(updatedSecret, Substitute.For<Response>()));

        // Act
        await _service.SetSecretAsync("test-secret", "updated-value");
        var result = await _service.GetSecretAsync("test-secret");

        // Assert
        Assert.Equal("updated-value", result);

        // SecretClient.GetSecretAsync should be called twice (cache was evicted)
        await _secretClient.Received(2)
            .GetSecretAsync("test-secret", cancellationToken: Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task DeleteSecretAsync_EvictsCache()
    {
        // Arrange
        var secret = new KeyVaultSecret("test-secret", "secret-value");
        _secretClient.GetSecretAsync("test-secret", cancellationToken: Arg.Any<CancellationToken>())
            .Returns(Response.FromValue(secret, Substitute.For<Response>()));

        // Populate cache
        await _service.GetSecretAsync("test-secret");

        // Act
        await _service.DeleteSecretAsync("test-secret");

        // Reconfigure for re-fetch after eviction
        var freshSecret = new KeyVaultSecret("test-secret", "fresh-value");
        _secretClient.GetSecretAsync("test-secret", cancellationToken: Arg.Any<CancellationToken>())
            .Returns(Response.FromValue(freshSecret, Substitute.For<Response>()));

        var result = await _service.GetSecretAsync("test-secret");

        // Assert
        Assert.Equal("fresh-value", result);

        // GetSecretAsync called twice (original + after eviction)
        await _secretClient.Received(2)
            .GetSecretAsync("test-secret", cancellationToken: Arg.Any<CancellationToken>());
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public async Task GetSecretAsync_WithNullOrEmptyName_ThrowsArgumentException(string? name)
    {
        await Assert.ThrowsAnyAsync<ArgumentException>(() => _service.GetSecretAsync(name!));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public async Task SetSecretAsync_WithNullOrEmptyName_ThrowsArgumentException(string? name)
    {
        await Assert.ThrowsAnyAsync<ArgumentException>(() => _service.SetSecretAsync(name!, "value"));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public async Task SetSecretAsync_WithNullOrEmptyValue_ThrowsArgumentException(string? value)
    {
        await Assert.ThrowsAnyAsync<ArgumentException>(() => _service.SetSecretAsync("name", value!));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public async Task DeleteSecretAsync_WithNullOrEmptyName_ThrowsArgumentException(string? name)
    {
        await Assert.ThrowsAnyAsync<ArgumentException>(() => _service.DeleteSecretAsync(name!));
    }
}
