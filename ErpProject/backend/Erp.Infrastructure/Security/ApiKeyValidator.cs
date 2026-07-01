using Erp.Application.Common.Interfaces;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Logging;
using System.Security.Cryptography;
using System.Text;

namespace Erp.Infrastructure.Security;

/// <summary>
/// Implementación de IApiKeyValidator con Redis para caching y rate limiting
/// </summary>
public class ApiKeyValidator : IApiKeyValidator
{
    private readonly IDistributedCache _cache;
    private readonly ILogger<ApiKeyValidator> _logger;
    private const string API_KEY_PREFIX = "api_key:";
    private const string RATE_LIMIT_PREFIX = "rate_limit:";

    public ApiKeyValidator(
        IDistributedCache cache,
        ILogger<ApiKeyValidator> logger)
    {
        _cache = cache;
        _logger = logger;
    }

    /// <summary>
    /// Valida una API Key verificando que exista y esté activa
    /// </summary>
    public async Task<bool> ValidateAsync(string apiKey)
    {
        if (string.IsNullOrEmpty(apiKey))
            return false;

        try
        {
            // Buscar en cache primero
            var cacheKey = $"{API_KEY_PREFIX}{HashApiKey(apiKey)}";
            var cachedInfo = await _cache.GetStringAsync(cacheKey);

            if (!string.IsNullOrEmpty(cachedInfo))
            {
                var info = System.Text.Json.JsonSerializer.Deserialize<ApiKeyInfo>(cachedInfo);
                return info?.IsActive ?? false;
            }

            _logger.LogWarning("API Key no encontrada o expirada: {ApiKeyPrefix}", apiKey[..4]);
            return false;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error validando API Key");
            return false;
        }
    }

    /// <summary>
    /// Obtiene información de una API Key
    /// </summary>
    public async Task<ApiKeyInfo?> GetApiKeyInfoAsync(string apiKey)
    {
        if (string.IsNullOrEmpty(apiKey))
            return null;

        try
        {
            var cacheKey = $"{API_KEY_PREFIX}{HashApiKey(apiKey)}";
            var cachedInfo = await _cache.GetStringAsync(cacheKey);

            if (string.IsNullOrEmpty(cachedInfo))
                return null;

            return System.Text.Json.JsonSerializer.Deserialize<ApiKeyInfo>(cachedInfo);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error obteniendo info de API Key");
            return null;
        }
    }

    /// <summary>
    /// Registra el uso de una API Key para rate limiting
    /// </summary>
    public async Task<bool> LogUsageAsync(string apiKey)
    {
        try
        {
            var info = await GetApiKeyInfoAsync(apiKey);
            if (info == null || !info.IsActive)
                return false;

            // Obtener contador actual
            var rateLimitKey = $"{RATE_LIMIT_PREFIX}{HashApiKey(apiKey)}";
            var countStr = await _cache.GetStringAsync(rateLimitKey);
            var count = string.IsNullOrEmpty(countStr) ? 0 : int.Parse(countStr);

            // Incrementar contador
            if (count >= info.RateLimit)
            {
                _logger.LogWarning("Rate limit excedido para API Key: {ApiKeyPrefix}", apiKey[..4]);
                return false;
            }

            // Incrementar y establecer expiración de 1 minuto
            await _cache.SetStringAsync(
                rateLimitKey,
                (count + 1).ToString(),
                new DistributedCacheEntryOptions { AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(1) }
            );

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error registrando uso de API Key");
            return false;
        }
    }

    /// <summary>
    /// Crea una nueva API Key
    /// </summary>
    public async Task<string> CreateApiKeyAsync(string name, int rateLimit)
    {
        try
        {
            // Generar clave aleatoria
            var key = GenerateApiKey();
            var keyHash = HashApiKey(key);
            var prefix = key[..4]; // Primeros 4 caracteres para mostrar

            // Crear info de la key
            var info = new ApiKeyInfo(
                Id: Guid.NewGuid().ToString(),
                Name: name,
                KeyPrefix: prefix,
                RateLimit: rateLimit,
                IsActive: true,
                CreatedAt: DateTime.UtcNow,
                LastUsedAt: null,
                RequestsThisMinute: 0
            );

            // Guardar en cache (válida por 1 año)
            var cacheKey = $"{API_KEY_PREFIX}{keyHash}";
            await _cache.SetStringAsync(
                cacheKey,
                System.Text.Json.JsonSerializer.Serialize(info),
                new DistributedCacheEntryOptions { AbsoluteExpirationRelativeToNow = TimeSpan.FromDays(365) }
            );

            _logger.LogInformation("Nueva API Key creada: {Name}", name);
            return key; // Retornar la clave completa solo una vez
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creando API Key");
            throw;
        }
    }

    /// <summary>
    /// Revoca una API Key (la deja inactiva)
    /// </summary>
    public async Task<bool> RevokeApiKeyAsync(string apiKey)
    {
        try
        {
            var info = await GetApiKeyInfoAsync(apiKey);
            if (info == null)
                return false;

            // Crear info inactiva
            var revokedInfo = new ApiKeyInfo(
                Id: info.Id,
                Name: info.Name,
                KeyPrefix: info.KeyPrefix,
                RateLimit: info.RateLimit,
                IsActive: false, // Cambiar a inactivo
                CreatedAt: info.CreatedAt,
                LastUsedAt: info.LastUsedAt,
                RequestsThisMinute: info.RequestsThisMinute
            );

            // Actualizar en cache
            var cacheKey = $"{API_KEY_PREFIX}{HashApiKey(apiKey)}";
            await _cache.SetStringAsync(
                cacheKey,
                System.Text.Json.JsonSerializer.Serialize(revokedInfo),
                new DistributedCacheEntryOptions { AbsoluteExpirationRelativeToNow = TimeSpan.FromDays(365) }
            );

            _logger.LogInformation("API Key revocada: {KeyPrefix}", apiKey[..4]);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error revocando API Key");
            return false;
        }
    }

    // ???????????????????????????????????????????????????????????????????
    // MÉTODOS PRIVADOS
    // ???????????????????????????????????????????????????????????????????

    /// <summary>
    /// Genera una nueva API Key aleatoria
    /// Formato: sk_live_XXXXXXXXXXXXXXXXXXXXXXXX (32 caracteres)
    /// </summary>
    private static string GenerateApiKey()
    {
        using var rng = new RNGCryptoServiceProvider();
        var tokenData = new byte[24];
        rng.GetBytes(tokenData);
        var token = Convert.ToBase64String(tokenData)
            .Replace("+", "")
            .Replace("/", "")
            .Replace("=", "")
            .Substring(0, 24);

        return $"sk_live_{token}";
    }

    /// <summary>
    /// Genera un hash SHA256 de una API Key para almacenarla de forma segura
    /// </summary>
    private static string HashApiKey(string apiKey)
    {
        using var sha256 = SHA256.Create();
        var hashedBytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(apiKey));
        return Convert.ToBase64String(hashedBytes);
    }
}
