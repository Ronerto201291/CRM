using Erp.Application.Common.Interfaces;
using Microsoft.Extensions.Configuration;

namespace Erp.Infrastructure.Services;

/// <summary>
/// Implementación de IPortalUrlProvider que lee la URL base del portal desde la configuración.
/// </summary>
public class PortalUrlProvider : IPortalUrlProvider
{
    private readonly string _baseUrl;

    public PortalUrlProvider(IConfiguration configuration)
    {
        // Leer Email:PortalBaseUrl con fallback a Email:AppBaseUrl y luego un default
        _baseUrl = configuration["Email:PortalBaseUrl"]
            ?? configuration["Email:AppBaseUrl"]
            ?? "https://app.example.com";
    }

    public string PortalBaseUrl => _baseUrl.TrimEnd('/');
}
