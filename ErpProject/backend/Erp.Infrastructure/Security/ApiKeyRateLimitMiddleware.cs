using Erp.Application.Common.Interfaces;
using Erp.Infrastructure.Data;
using Erp.Infrastructure.Resilience;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace Erp.Infrastructure.Security;

/// <summary>
/// Valida X-Api-Key contra la tabla ApiKeys, aplica rate limit Redis y fija el tenant activo.
/// Cubre todo /api/v1/** salvo health y portal de presupuestos por token (ADR-0016 / ADR-0018).
/// </summary>
public class ApiKeyRateLimitMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<ApiKeyRateLimitMiddleware> _logger;

    public ApiKeyRateLimitMiddleware(RequestDelegate next, ILogger<ApiKeyRateLimitMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context, IServiceProvider services, ITenantContext tenantContext)
    {
        if (!PublicApiPaths.RequiresApiKey(context.Request.Path))
        {
            await _next(context);
            return;
        }

        var apiKeyHeader = context.Request.Headers["X-Api-Key"].FirstOrDefault()
            ?? context.Request.Headers["X-API-Key"].FirstOrDefault();

        if (string.IsNullOrEmpty(apiKeyHeader))
        {
            await WriteJsonError(context, 401, "X-Api-Key header required for public API access.");
            return;
        }

        using var scope = services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ErpDbContext>();
        var cache = scope.ServiceProvider.GetRequiredService<IDistributedCache>();

        var incomingKeyHash = ComputeKeyHash(apiKeyHeader);

        var apiKey = await db.ApiKeys
            .IgnoreQueryFilters()
            .AsNoTracking()
            .FirstOrDefaultAsync(k => k.KeyHash == incomingKeyHash && k.IsActive);

        if (apiKey == null)
        {
            await WriteJsonError(context, 401, "Invalid or inactive API key.");
            return;
        }

        var cacheKey = $"ratelimit:apikey:{apiKey.Id}:{DateTime.UtcNow:yyyyMMddHHmm}";
        var countBytes = await RedisResilienceHelper.GetAsync(cache, cacheKey, _logger);
        var currentCount = countBytes != null ? BitConverter.ToInt32(countBytes) : 0;

        if (currentCount >= apiKey.RateLimit)
        {
            _logger.LogWarning("Rate limit exceeded for ApiKey {KeyId} (Company {CompanyId}): {Count}/{Limit}",
                apiKey.Id, apiKey.CompanyId, currentCount, apiKey.RateLimit);

            context.Response.StatusCode = 429;
            context.Response.ContentType = "application/json";
            context.Response.Headers["Retry-After"] = "60";
            context.Response.Headers["X-RateLimit-Limit"] = apiKey.RateLimit.ToString();
            context.Response.Headers["X-RateLimit-Remaining"] = "0";
            await context.Response.WriteAsync(JsonSerializer.Serialize(new
            {
                error = "Rate limit exceeded. Maximum requests per minute: " + apiKey.RateLimit,
                retryAfterSeconds = 60
            }));
            return;
        }

        var newCount = currentCount + 1;
        await RedisResilienceHelper.SetAsync(cache, cacheKey,
            BitConverter.GetBytes(newCount),
            new DistributedCacheEntryOptions { AbsoluteExpirationRelativeToNow = TimeSpan.FromSeconds(70) },
            _logger);

        db.ApiUsageLogs.Add(new Erp.Domain.Entities.Api.ApiUsageLog
        {
            Id = Guid.NewGuid(),
            ApiKeyId = apiKey.Id,
            Endpoint = $"{context.Request.Method} {context.Request.Path}",
            Timestamp = DateTime.UtcNow
        });
        await db.SaveChangesAsync();

        context.Response.Headers["X-RateLimit-Limit"] = apiKey.RateLimit.ToString();
        context.Response.Headers["X-RateLimit-Remaining"] = (apiKey.RateLimit - newCount).ToString();

        context.Items[PublicApiPaths.ItemApiKeyId] = apiKey.Id;
        context.Items[PublicApiPaths.ItemApiKeyCompanyId] = apiKey.CompanyId;
        tenantContext.SetTenant(apiKey.CompanyId, "ApiKey");

        await _next(context);
    }

    private static async Task WriteJsonError(HttpContext context, int status, string message)
    {
        context.Response.StatusCode = status;
        context.Response.ContentType = "application/json";
        await context.Response.WriteAsync(JsonSerializer.Serialize(new { error = message }));
    }

    private static string ComputeKeyHash(string key)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(key));
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }
}
