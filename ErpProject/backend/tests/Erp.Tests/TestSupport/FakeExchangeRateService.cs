using Erp.Modules.Treasury.Application.Interfaces;

namespace Erp.Tests.TestSupport;

public sealed class FakeExchangeRateService : IExchangeRateService
{
    public Task<decimal> GetRateAsync(string from, string to, CancellationToken ct = default)
        => Task.FromResult(1.08m);

    public Task RefreshRatesAsync(CancellationToken ct = default) => Task.CompletedTask;

    public Task RefreshAllTenantsRatesAsync(CancellationToken ct = default) => Task.CompletedTask;
}
