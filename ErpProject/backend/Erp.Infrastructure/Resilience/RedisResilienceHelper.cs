using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Logging;
using StackExchange.Redis;

namespace Erp.Infrastructure.Resilience;

/// <summary>
/// Wraps IDistributedCache operations to fail open when Redis is unavailable.
/// A Redis outage logs a warning and returns the provided fallback value
/// instead of propagating the exception through the request pipeline.
/// </summary>
public static class RedisResilienceHelper
{
    public static async Task<byte[]?> GetAsync(
        IDistributedCache cache,
        string key,
        ILogger logger,
        CancellationToken ct = default)
    {
        try
        {
            return await cache.GetAsync(key, ct);
        }
        catch (RedisConnectionException ex)
        {
            logger.LogWarning(ex, "Redis unavailable reading key {Key} — failing open", key);
            return null;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogWarning(ex, "Redis error reading key {Key} — failing open", key);
            return null;
        }
    }

    public static async Task SetAsync(
        IDistributedCache cache,
        string key,
        byte[] value,
        DistributedCacheEntryOptions options,
        ILogger logger,
        CancellationToken ct = default)
    {
        try
        {
            await cache.SetAsync(key, value, options, ct);
        }
        catch (RedisConnectionException ex)
        {
            logger.LogWarning(ex, "Redis unavailable writing key {Key} — skipping", key);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogWarning(ex, "Redis error writing key {Key} — skipping", key);
        }
    }
}
