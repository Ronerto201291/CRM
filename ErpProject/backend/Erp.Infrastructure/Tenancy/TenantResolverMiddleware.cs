using Erp.Application.Common.Interfaces;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Erp.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Erp.Infrastructure.Tenancy;

public class TenantResolverMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<TenantResolverMiddleware> _logger;

    public TenantResolverMiddleware(RequestDelegate next, ILogger<TenantResolverMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context, ITenantContext tenantContext, ErpDbContext dbContext)
    {
        Guid? resolvedTenantId = null;
        string? resolvedTenantName = null;

        // 1. Try X-Tenant-Id header (Primary for API)
        if (context.Request.Headers.TryGetValue("X-Tenant-Id", out var tenantIdHeader))
        {
            if (Guid.TryParse(tenantIdHeader, out var tenantId))
            {
                var company = await dbContext.Companies
                    .AsNoTracking()
                    .FirstOrDefaultAsync(c => c.Id == tenantId && c.IsActive);

                if (company != null)
                {
                    resolvedTenantId = tenantId;
                    resolvedTenantName = company.Name;
                    _logger.LogInformation("? Tenant resolved from X-Tenant-Id header: {TenantId} ({TenantName})", tenantId, company.Name);
                }
                else
                {
                    _logger.LogWarning("?? Tenant ID from header not found or inactive: {TenantId}", tenantId);
                    context.Response.StatusCode = 401;
                    await context.Response.WriteAsJsonAsync(new { error = "Tenant inv�lido o inactivo" });
                    return;
                }
            }
            else
            {
                _logger.LogWarning("?? Invalid Tenant ID format: {TenantIdHeader}", tenantIdHeader);
                context.Response.StatusCode = 400;
                await context.Response.WriteAsJsonAsync(new { error = "X-Tenant-Id debe ser un GUID v�lido" });
                return;
            }
        }
        // 2. Try subdomain (Multi-tenant SaaS style: tenant.domain.com)
        else if (!string.IsNullOrEmpty(context.Request.Host.Host))
        {
            var host = context.Request.Host.Host;
            if (host != "localhost" && host != "127.0.0.1" && !host.StartsWith("www."))
            {
                var subdomain = host.Split('.')[0];
                var company = await dbContext.Companies
                    .AsNoTracking()
                    .FirstOrDefaultAsync(c => c.Name.ToLower() == subdomain.ToLower() && c.IsActive);

                if (company != null)
                {
                    resolvedTenantId = company.Id;
                    resolvedTenantName = company.Name;
                    _logger.LogInformation("? Tenant resolved from subdomain: {Subdomain} ({TenantId})", subdomain, company.Id);
                }
            }
        }

        // 3. Si no se resolvi� tenant y la ruta requiere autenticaci�n, rechazar
        if (resolvedTenantId == null && IsProtectedRoute(context.Request.Path))
        {
            _logger.LogWarning("? No tenant resolved for protected route: {Path}", context.Request.Path);
            context.Response.StatusCode = 401;
            await context.Response.WriteAsJsonAsync(new { error = "Tenant ID requerido" });
            return;
        }

        // 4. Configurar tenant context
        if (resolvedTenantId.HasValue)
        {
            tenantContext.SetTenant(resolvedTenantId.Value, resolvedTenantName ?? "Unknown");
        }

        await _next(context);
    }

    /// <summary>
    /// Determina si una ruta requiere resoluci�n de tenant
    /// </summary>
    private bool IsProtectedRoute(PathString path)
    {
        var publicPaths = new[]
        {
            "/api/auth/login",
            "/api/auth/register",
            "/api/auth/refresh",
            "/api/expenses/upload",  // Público pero con token
            "/api/stripe/webhook",   // Stripe webhook - no tenant context required
            "/health",               // Health checks - no tenant required
            "/metrics",              // Prometheus scrape (ADR-0018 #36)
            "/swagger",              // Swagger UI
            "/hangfire"              // Hangfire dashboard
        };

        return !publicPaths.Any(p => path.StartsWithSegments(p, StringComparison.OrdinalIgnoreCase));
    }
}
