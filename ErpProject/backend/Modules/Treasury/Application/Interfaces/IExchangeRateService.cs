namespace Erp.Modules.Treasury.Application.Interfaces;

public interface IExchangeRateService
{
    Task<decimal> GetRateAsync(string from, string to, CancellationToken ct = default);
    Task RefreshRatesAsync(CancellationToken ct = default);
    /// <summary>Refresca tipos de cambio para todas las empresas (jobs en background sin contexto HTTP).</summary>
    Task RefreshAllTenantsRatesAsync(CancellationToken ct = default);
}
