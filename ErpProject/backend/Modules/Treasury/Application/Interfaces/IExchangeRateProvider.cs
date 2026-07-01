namespace Erp.Modules.Treasury.Application.Interfaces;

public interface IExchangeRateProvider
{
    string ProviderName { get; }
    Task<ExchangeRateResult> GetRatesAsync(CancellationToken ct = default);
}

public record ExchangeRateResult(
    DateTime Date,
    string BaseCurrency,
    Dictionary<string, decimal> Rates);
