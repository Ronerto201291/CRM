using Erp.Application.Common;
using Microsoft.AspNetCore.Http;

namespace Erp.Infrastructure.Security;

/// <summary>
/// Rutas de API pública v1 y claves de <see cref="Microsoft.AspNetCore.Http.HttpContext.Items"/>.
/// </summary>
public static class PublicApiPaths
{
    public const string ItemApiKeyId = PublicApiContextKeys.ApiKeyId;
    public const string ItemApiKeyCompanyId = PublicApiContextKeys.CompanyId;

    /// <summary>
    /// Rutas bajo /api/v1 que exigen X-Api-Key (salvo health y los portales públicos por
    /// token de presupuestos/facturas/subida de facturas de proveedor — ADR-0018 #39).
    /// </summary>
    public static bool RequiresApiKey(PathString path)
    {
        var p = path.Value ?? string.Empty;
        if (!p.StartsWith("/api/v1", StringComparison.OrdinalIgnoreCase))
            return false;
        if (p.StartsWith("/api/v1/health", StringComparison.OrdinalIgnoreCase))
            return false;
        if (p.StartsWith("/api/v1/public/quotes", StringComparison.OrdinalIgnoreCase))
            return false;
        if (p.StartsWith("/api/v1/public/invoice-view", StringComparison.OrdinalIgnoreCase))
            return false;
        if (p.StartsWith("/api/v1/public/supplier-uploads", StringComparison.OrdinalIgnoreCase))
            return false;
        return true;
    }
}
