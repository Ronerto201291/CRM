namespace Erp.Application.Common.Interfaces;

/// <summary>
/// Conversión de divisas para módulos que no referencian Treasury directamente (ADR-0018 #38).
/// Implementado por Treasury.Infrastructure usando tipos de cambio del tenant.
/// </summary>
public interface IExchangeRateLookup
{
    /// <summary>Tipo de cambio de <paramref name="currencyCode"/> a EUR (1 unidad de divisa → EUR).</summary>
    Task<decimal> GetRateToEurAsync(string currencyCode, CancellationToken ct = default);

    /// <summary>Convierte un importe en la divisa indicada a EUR.</summary>
    Task<decimal> ConvertToEurAsync(string currencyCode, decimal amount, CancellationToken ct = default);
}
