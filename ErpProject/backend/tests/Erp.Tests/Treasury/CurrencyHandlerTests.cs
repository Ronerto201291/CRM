using Erp.Modules.Treasury.Application.Features.Currencies;
using Erp.Modules.Treasury.Domain.Entities;
using Erp.Modules.Treasury.Infrastructure.Data;
using Erp.Tests.TestSupport;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Erp.Tests.Treasury;

public class CreateCurrencyHandlerTests
{
    [Fact]
    public async Task Handle_PersistsCurrencyForTenant()
    {
        var companyId = Guid.NewGuid();
        var tenant = new FakeTenantContext();
        tenant.SetTenant(companyId, "Empresa test");

        var options = new DbContextOptionsBuilder<TreasuryDbContext>()
            .UseInMemoryDatabase($"treasury-create-currency-{Guid.NewGuid()}")
            .Options;

        await using var ctx = new TreasuryDbContext(options, tenant);
        var handler = new CreateCurrencyHandler(ctx, tenant);

        var result = await handler.Handle(new CreateCurrencyCommand("gbp", "Libra esterlina", 0.86m), CancellationToken.None);

        Assert.Equal("GBP", result.Code);
        Assert.Equal(0.86m, result.ExchangeRate);
        Assert.Equal(1, await ctx.Currencies.CountAsync());
    }

    [Fact]
    public async Task Handle_Throws_WhenCurrencyAlreadyExists()
    {
        var companyId = Guid.NewGuid();
        var tenant = new FakeTenantContext();
        tenant.SetTenant(companyId, "Empresa test");

        var options = new DbContextOptionsBuilder<TreasuryDbContext>()
            .UseInMemoryDatabase($"treasury-dup-currency-{Guid.NewGuid()}")
            .Options;

        await using var ctx = new TreasuryDbContext(options, tenant);
        ctx.Currencies.Add(new Currency
        {
            Id = Guid.NewGuid(),
            CompanyId = companyId,
            Code = "USD",
            Name = "Dólar",
            ExchangeRate = 1.08m,
            RateDate = DateTime.UtcNow,
            Source = "Manual",
            IsActive = true,
        });
        await ctx.SaveChangesAsync();

        var handler = new CreateCurrencyHandler(ctx, tenant);
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            handler.Handle(new CreateCurrencyCommand("USD", "Duplicado", 1m), CancellationToken.None));
    }
}

public class ExchangeCurrencyHandlerTests
{
    [Fact]
    public async Task Handle_ConvertsAmountUsingStoredRates()
    {
        var companyId = Guid.NewGuid();
        var tenant = new FakeTenantContext();
        tenant.SetTenant(companyId, "Empresa test");

        var options = new DbContextOptionsBuilder<TreasuryDbContext>()
            .UseInMemoryDatabase($"treasury-exchange-{Guid.NewGuid()}")
            .Options;

        await using var ctx = new TreasuryDbContext(options, tenant);
        ctx.Currencies.AddRange(
            new Currency
            {
                Id = Guid.NewGuid(),
                CompanyId = companyId,
                Code = "EUR",
                Name = "Euro",
                ExchangeRate = 1m,
                RateDate = DateTime.UtcNow,
                Source = "Manual",
                IsActive = true,
            },
            new Currency
            {
                Id = Guid.NewGuid(),
                CompanyId = companyId,
                Code = "USD",
                Name = "Dólar",
                ExchangeRate = 1.08m,
                RateDate = DateTime.UtcNow,
                Source = "Manual",
                IsActive = true,
            });
        await ctx.SaveChangesAsync();

        var handler = new ExchangeCurrencyHandler(ctx, tenant, new FakeExchangeRateService());
        var result = await handler.Handle(
            new ExchangeCurrencyCommand("EUR", "USD", 100m, "Manual"),
            CancellationToken.None);

        Assert.Equal(108m, result.ExchangedAmount);
        Assert.Equal(1.08m, result.Rate);
    }
}

public class DeleteCurrencyHandlerTests
{
    [Fact]
    public async Task Handle_SoftDeletesCurrency()
    {
        var companyId = Guid.NewGuid();
        var currencyId = Guid.NewGuid();
        var tenant = new FakeTenantContext();
        tenant.SetTenant(companyId, "Empresa test");

        var options = new DbContextOptionsBuilder<TreasuryDbContext>()
            .UseInMemoryDatabase($"treasury-delete-currency-{Guid.NewGuid()}")
            .Options;

        await using var ctx = new TreasuryDbContext(options, tenant);
        ctx.Currencies.Add(new Currency
        {
            Id = currencyId,
            CompanyId = companyId,
            Code = "CHF",
            Name = "Franco",
            ExchangeRate = 0.95m,
            RateDate = DateTime.UtcNow,
            Source = "Manual",
            IsActive = true,
        });
        await ctx.SaveChangesAsync();

        var handler = new DeleteCurrencyHandler(ctx, tenant);
        var ok = await handler.Handle(new DeleteCurrencyCommand(currencyId), CancellationToken.None);

        Assert.True(ok);
        var currency = await ctx.Currencies.SingleAsync();
        Assert.False(currency.IsActive);
    }
}
