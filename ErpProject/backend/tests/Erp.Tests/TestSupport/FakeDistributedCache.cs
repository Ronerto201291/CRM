using Microsoft.Extensions.Caching.Distributed;
using System.Text;

namespace Erp.Tests.TestSupport;

public sealed class FakeDistributedCache : IDistributedCache
{
    private readonly Dictionary<string, byte[]> _store = new();

    public byte[]? Get(string key) => _store.GetValueOrDefault(key);

    public Task<byte[]?> GetAsync(string key, CancellationToken token = default)
        => Task.FromResult(Get(key));

    public void Set(string key, byte[] value, DistributedCacheEntryOptions options)
        => _store[key] = value;

    public Task SetAsync(string key, byte[] value, DistributedCacheEntryOptions options, CancellationToken token = default)
    {
        Set(key, value, options);
        return Task.CompletedTask;
    }

    public void SetString(string key, string value, DistributedCacheEntryOptions options)
        => Set(key, Encoding.UTF8.GetBytes(value), options);

    public Task SetStringAsync(string key, string value, DistributedCacheEntryOptions options, CancellationToken token = default)
    {
        SetString(key, value, options);
        return Task.CompletedTask;
    }

    public string? GetString(string key)
    {
        var bytes = Get(key);
        return bytes is null ? null : Encoding.UTF8.GetString(bytes);
    }

    public Task<string?> GetStringAsync(string key, CancellationToken token = default)
        => Task.FromResult(GetString(key));

    public void Remove(string key) => _store.Remove(key);

    public Task RemoveAsync(string key, CancellationToken token = default)
    {
        Remove(key);
        return Task.CompletedTask;
    }

    public void Refresh(string key) { }

    public Task RefreshAsync(string key, CancellationToken token = default) => Task.CompletedTask;
}
