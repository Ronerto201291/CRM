namespace Erp.Modules.Treasury.Application.Interfaces;

public interface IExchangeRateService
{
    Task<decimal> GetRateAsync(string from, string to, CancellationToken ct = default);
    Task RefreshRatesAsync(CancellationToken ct = default);
}
