namespace Erp.Application.Common.Interfaces;

/// <summary>
/// Servicio para validar y gestionar API Keys
/// </summary>
public interface IApiKeyValidator
{
    /// <summary>
    /// Valida una API Key
    /// </summary>
    /// <param name="apiKey">La clave API a validar</param>
    /// <returns>true si es válida y activa, false si no</returns>
    Task<bool> ValidateAsync(string apiKey);

    /// <summary>
    /// Obtiene los detalles de una API Key
    /// </summary>
    /// <param name="apiKey">La clave API</param>
    /// <returns>Información de la API Key o null si no existe</returns>
    Task<ApiKeyInfo?> GetApiKeyInfoAsync(string apiKey);

    /// <summary>
    /// Registra el uso de una API Key para rate limiting
    /// </summary>
    /// <param name="apiKey">La clave API usada</param>
    /// <returns>true si está dentro del límite de rate</returns>
    Task<bool> LogUsageAsync(string apiKey);

    /// <summary>
    /// Crea una nueva API Key
    /// </summary>
    /// <param name="name">Nombre descriptivo</param>
    /// <param name="rateLimit">Límite de requests por minuto</param>
    /// <returns>La clave completa (solo se muestra una vez)</returns>
    Task<string> CreateApiKeyAsync(string name, int rateLimit);

    /// <summary>
    /// Revoca una API Key
    /// </summary>
    /// <param name="apiKey">La clave API a revocar</param>
    Task<bool> RevokeApiKeyAsync(string apiKey);
}

/// <summary>
/// Información de una API Key
/// </summary>
public record ApiKeyInfo(
    string Id,
    string Name,
    string KeyPrefix, // Los primeros caracteres, sin la clave completa
    int RateLimit,
    bool IsActive,
    DateTime CreatedAt,
    DateTime? LastUsedAt,
    int RequestsThisMinute
);
