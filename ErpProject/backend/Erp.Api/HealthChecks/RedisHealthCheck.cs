using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace Erp.Api.HealthChecks;

/// <summary>
/// Verifies Redis connectivity by writing and reading a probe key via IDistributedCache.
/// </summary>
public class RedisHealthCheck : IHealthCheck
{
    private readonly IDistributedCache _cache;

    public RedisHealthCheck(IDistributedCache cache) => _cache = cache;

    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context, CancellationToken ct = default)
    {
        try
        {
            const string key = "hc_redis_probe";
            await _cache.SetStringAsync(key, "1",
                new DistributedCacheEntryOptions
                {
                    AbsoluteExpirationRelativeToNow = TimeSpan.FromSeconds(10)
                }, ct);

            var value = await _cache.GetStringAsync(key, ct);
            return value == "1"
                ? HealthCheckResult.Healthy("Redis is reachable.")
                : HealthCheckResult.Degraded("Redis probe returned unexpected value.");
        }
        catch (Exception ex)
        {
            return HealthCheckResult.Unhealthy("Redis unreachable.", ex);
        }
    }
}
