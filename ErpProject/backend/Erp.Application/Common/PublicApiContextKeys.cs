namespace Erp.Application.Common;

/// <summary>
/// Claves de <see cref="Microsoft.AspNetCore.Http.HttpContext.Items"/> usadas por la API pública.
/// Deben coincidir con <see cref="Erp.Infrastructure.Security.PublicApiPaths"/>.
/// </summary>
public static class PublicApiContextKeys
{
    public const string ApiKeyId = "PublicApi:ApiKeyId";
    public const string CompanyId = "PublicApi:CompanyId";
}
