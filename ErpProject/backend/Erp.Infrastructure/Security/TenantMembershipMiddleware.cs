using System.Security.Claims;
using Erp.Application.Common.Interfaces;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Erp.Infrastructure.Security;

/// <summary>
/// Tras autenticación, valida que el usuario tenga membresía en el tenant resuelto (ADR-0002 / #42a).
/// </summary>
public class TenantMembershipMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<TenantMembershipMiddleware> _logger;

    public TenantMembershipMiddleware(RequestDelegate next, ILogger<TenantMembershipMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context, IApplicationDbContext db, ITenantContext tenantContext)
    {
        if (context.User.Identity?.IsAuthenticated == true && tenantContext.TenantId.HasValue)
        {
            var userIdClaim = context.User.FindFirst(ClaimTypes.NameIdentifier)?.Value
                           ?? context.User.FindFirst("sub")?.Value;
            var jwtCompanyClaim = context.User.FindFirst("CompanyId")?.Value;

            if (Guid.TryParse(userIdClaim, out var userId))
            {
                var tenantId = tenantContext.TenantId.Value;

                if (Guid.TryParse(jwtCompanyClaim, out var jwtCompany) && jwtCompany != tenantId)
                {
                    _logger.LogWarning("JWT CompanyId {JwtCompany} no coincide con X-Tenant-Id {Tenant}", jwtCompany, tenantId);
                    context.Response.StatusCode = 403;
                    await context.Response.WriteAsJsonAsync(new { error = "Empresa activa no coincide con el tenant solicitado" });
                    return;
                }

                var hasMembership = await db.UserCompanies.IgnoreQueryFilters()
                    .AnyAsync(uc => uc.UserId == userId && uc.CompanyId == tenantId);

                if (!hasMembership)
                {
                    var legacy = await db.Users.IgnoreQueryFilters()
                        .AnyAsync(u => u.Id == userId && u.CompanyId == tenantId);

                    if (!legacy)
                    {
                        _logger.LogWarning("Usuario {UserId} sin acceso a tenant {TenantId}", userId, tenantId);
                        context.Response.StatusCode = 403;
                        await context.Response.WriteAsJsonAsync(new { error = "Sin acceso a esta empresa" });
                        return;
                    }
                }
            }
        }

        await _next(context);
    }
}
