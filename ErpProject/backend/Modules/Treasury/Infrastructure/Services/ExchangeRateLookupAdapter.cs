using Erp.Application.Common.Interfaces;
using Erp.Modules.Treasury.Application.Interfaces;

namespace Erp.Modules.Treasury.Infrastructure.Services;

public sealed class ExchangeRateLookupAdapter(IExchangeRateService rates) : IExchangeRateLookup
{
    public async Task<decimal> GetRateToEurAsync(string currencyCode, CancellationToken ct = default)
    {
        var code = (currencyCode ?? "EUR").Trim().ToUpperInvariant();
        if (code == "EUR") return 1m;
        return await rates.GetRateAsync(code, "EUR", ct);
    }

    public async Task<decimal> ConvertToEurAsync(string currencyCode, decimal amount, CancellationToken ct = default)
    {
        var rate = await GetRateToEurAsync(currencyCode, ct);
        return Math.Round(amount * rate, 2, MidpointRounding.AwayFromZero);
    }
}
