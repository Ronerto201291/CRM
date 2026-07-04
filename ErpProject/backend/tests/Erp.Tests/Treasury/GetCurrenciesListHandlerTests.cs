using Erp.Modules.Treasury.Application.Features.Currencies;
using Erp.Modules.Treasury.Domain.Entities;
using Erp.Modules.Treasury.Infrastructure.Data;
using Erp.Tests.TestSupport;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Erp.Tests.Treasury;

public class GetCurrencyRatesHandlerTests
{
    [Fact]
    public async Task Handle_ReturnsDictionaryOfActiveRates()
    {
        var companyId = Guid.NewGuid();
        var tenant = new FakeTenantContext();
        tenant.SetTenant(companyId, "Empresa test");

        var options = new DbContextOptionsBuilder<TreasuryDbContext>()
            .UseInMemoryDatabase($"currency-rates-{Guid.NewGuid()}")
            .Options;

        await using var ctx = new TreasuryDbContext(options, tenant);
        ctx.Currencies.AddRange(
            new Currency
            {
                Id = Guid.NewGuid(), CompanyId = companyId,
                Code = "EUR", Name = "Euro", ExchangeRate = 1m,
                RateDate = DateTime.UtcNow, Source = "Manual", IsActive = true,
            },
            new Currency
            {
                Id = Guid.NewGuid(), CompanyId = companyId,
                Code = "USD", Name = "Dólar", ExchangeRate = 1.08m,
                RateDate = DateTime.UtcNow, Source = "Manual", IsActive = true,
            });
        await ctx.SaveChangesAsync();

        var handler = new GetCurrencyRatesHandler(ctx, tenant);
        var rates = await handler.Handle(new GetCurrencyRatesQuery(), CancellationToken.None);

        Assert.Equal(2, rates.Count);
        Assert.Equal(1.08m, rates["USD"]);
    }
}

public class UpdateCurrencyRateHandlerTests
{
    [Fact]
    public async Task Handle_UpdatesExchangeRate()
    {
        var companyId = Guid.NewGuid();
        var currencyId = Guid.NewGuid();
        var tenant = new FakeTenantContext();
        tenant.SetTenant(companyId, "Empresa test");

        var options = new DbContextOptionsBuilder<TreasuryDbContext>()
            .UseInMemoryDatabase($"currency-rate-update-{Guid.NewGuid()}")
            .Options;

        await using var ctx = new TreasuryDbContext(options, tenant);
        ctx.Currencies.Add(new Currency
        {
            Id = currencyId, CompanyId = companyId,
            Code = "GBP", Name = "Libra", ExchangeRate = 0.86m,
            RateDate = DateTime.UtcNow, Source = "Manual", IsActive = true,
        });
        await ctx.SaveChangesAsync();

        var handler = new UpdateCurrencyRateHandler(ctx, tenant);
        var result = await handler.Handle(new UpdateCurrencyRateCommand(currencyId, 0.88m), CancellationToken.None);

        Assert.Equal(0.88m, result.ExchangeRate);
    }
}
