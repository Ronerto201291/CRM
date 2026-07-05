using Erp.Application.Common.Interfaces;

namespace Erp.Tests.TestSupport;

public sealed class FakeExchangeRateLookup : IExchangeRateLookup
{
    private readonly Dictionary<string, decimal> _rates;

    public FakeExchangeRateLookup(Dictionary<string, decimal>? rates = null)
    {
        _rates = rates ?? new Dictionary<string, decimal>(StringComparer.OrdinalIgnoreCase)
        {
            ["EUR"] = 1m,
            ["USD"] = 0.92m,
        };
    }

    public Task<decimal> GetRateToEurAsync(string currencyCode, CancellationToken ct = default)
    {
        var code = currencyCode.ToUpperInvariant();
        return Task.FromResult(_rates.TryGetValue(code, out var r) ? r : 1m);
    }

    public async Task<decimal> ConvertToEurAsync(string currencyCode, decimal amount, CancellationToken ct = default)
    {
        var rate = await GetRateToEurAsync(currencyCode, ct);
        return Math.Round(amount * rate, 2, MidpointRounding.AwayFromZero);
    }
}
