using Erp.Application.Common.Interfaces;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace Erp.Infrastructure.Security;

/// <summary>
/// Middleware que valida la API Key en requests a /api/v1
/// </summary>
public class ApiKeyValidationMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<ApiKeyValidationMiddleware> _logger;

    public ApiKeyValidationMiddleware(
        RequestDelegate next,
        ILogger<ApiKeyValidationMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context, IApiKeyValidator apiKeyValidator)
    {
        // Solo validar rutas de API pública /api/v1
        if (!context.Request.Path.StartsWithSegments("/api/v1"))
        {
            await _next(context);
            return;
        }

        // Permitir health check sin API Key
        if (context.Request.Path.StartsWithSegments("/api/v1/health"))
        {
            await _next(context);
            return;
        }

        // Obtener API Key del header
        var apiKey = context.Request.Headers["X-API-Key"].ToString();

        if (string.IsNullOrEmpty(apiKey))
        {
            _logger.LogWarning("Request sin API Key a {Path} desde {RemoteIp}",
                context.Request.Path, context.Connection.RemoteIpAddress);

            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            await context.Response.WriteAsJsonAsync(new { error = "API Key requerida" });
            return;
        }

        // Validar API Key
        var isValid = await apiKeyValidator.ValidateAsync(apiKey);
        if (!isValid)
        {
            _logger.LogWarning("API Key inválida para {Path} desde {RemoteIp}",
                context.Request.Path, context.Connection.RemoteIpAddress);

            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            await context.Response.WriteAsJsonAsync(new { error = "API Key inválida o expirada" });
            return;
        }

        // Validar rate limiting
        var isWithinRateLimit = await apiKeyValidator.LogUsageAsync(apiKey);
        if (!isWithinRateLimit)
        {
            _logger.LogWarning("Rate limit excedido para API Key de {RemoteIp}",
                context.Connection.RemoteIpAddress);

            context.Response.StatusCode = StatusCodes.Status429TooManyRequests;
            context.Response.Headers.Add("Retry-After", "60");
            await context.Response.WriteAsJsonAsync(new { error = "Rate limit excedido. Intenta en 1 minuto" });
            return;
        }

        await _next(context);
    }
}
