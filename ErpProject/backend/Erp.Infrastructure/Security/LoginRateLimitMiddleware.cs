using Erp.Infrastructure.Resilience;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System.Text.Json;

namespace Erp.Infrastructure.Security;

/// <summary>
/// Middleware that protects POST /api/auth/login against brute-force attacks.
/// Uses a Redis sliding-window counter keyed by client IP address.
/// Limit: 10 attempts per IP per minute → HTTP 429 with Retry-After: 60.
/// </summary>
public class LoginRateLimitMiddleware
{
    private const string LoginPath = "/api/auth/login";
    private const int MaxAttempts = 10;
    private const int WindowSeconds = 70; // slightly over 1 min to cover full window

    private readonly RequestDelegate _next;
    private readonly ILogger<LoginRateLimitMiddleware> _logger;

    public LoginRateLimitMiddleware(RequestDelegate next, ILogger<LoginRateLimitMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context, IServiceProvider services)
    {
        // Only intercept POST /api/auth/login
        if (!context.Request.Method.Equals("POST", StringComparison.OrdinalIgnoreCase) ||
            !context.Request.Path.Equals(LoginPath, StringComparison.OrdinalIgnoreCase))
        {
            await _next(context);
            return;
        }

        var ip = context.Connection.RemoteIpAddress?.ToString() ?? "unknown";

        // Respect X-Forwarded-For when behind Nginx reverse proxy
        if (context.Request.Headers.TryGetValue("X-Forwarded-For", out var forwarded))
        {
            var firstIp = forwarded.ToString().Split(',')[0].Trim();
            if (!string.IsNullOrEmpty(firstIp))
                ip = firstIp;
        }

        using var scope = services.CreateScope();
        var cache = scope.ServiceProvider.GetRequiredService<IDistributedCache>();

        // Sliding-window key per minute bucket (same pattern as ApiKeyRateLimitMiddleware)
        var cacheKey = $"ratelimit:login:{ip}:{DateTime.UtcNow:yyyyMMddHHmm}";
        var countBytes = await RedisResilienceHelper.GetAsync(cache, cacheKey, _logger);
        var currentCount = countBytes != null ? BitConverter.ToInt32(countBytes) : 0;

        if (currentCount >= MaxAttempts)
        {
            _logger.LogWarning(
                "Login brute-force blocked: IP={Ip} attempts={Count}/{Limit}",
                ip, currentCount, MaxAttempts);

            context.Response.StatusCode = 429;
            context.Response.ContentType = "application/json";
            context.Response.Headers["Retry-After"] = "60";
            context.Response.Headers["X-RateLimit-Limit"] = MaxAttempts.ToString();
            context.Response.Headers["X-RateLimit-Remaining"] = "0";
            await context.Response.WriteAsync(JsonSerializer.Serialize(new
            {
                error = "Demasiados intentos de inicio de sesión. Espera 1 minuto antes de volver a intentarlo.",
                retryAfterSeconds = 60
            }));
            return;
        }

        // Increment counter; reset TTL on each call (absolute window per minute)
        var newCount = currentCount + 1;
        await RedisResilienceHelper.SetAsync(
            cache,
            cacheKey,
            BitConverter.GetBytes(newCount),
            new DistributedCacheEntryOptions { AbsoluteExpirationRelativeToNow = TimeSpan.FromSeconds(WindowSeconds) },
            _logger);

        context.Response.Headers["X-RateLimit-Limit"] = MaxAttempts.ToString();
        context.Response.Headers["X-RateLimit-Remaining"] = (MaxAttempts - newCount).ToString();

        await _next(context);
    }
}
