using Erp.Modules.Treasury.Application.Features.Currencies;
using Erp.Modules.Treasury.Domain.Entities;
using Erp.Modules.Treasury.Infrastructure.Data;
using Erp.Tests.TestSupport;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Erp.Tests.Treasury;

public class GetCurrenciesHandlerTests
{
    [Fact]
    public async Task Handle_ReturnsCurrenciesForTenant()
    {
        var companyId = Guid.NewGuid();
        var tenant = new FakeTenantContext();
        tenant.SetTenant(companyId, "Empresa test");

        var options = new DbContextOptionsBuilder<TreasuryDbContext>()
            .UseInMemoryDatabase($"treasury-currencies-{Guid.NewGuid()}")
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
        });
        await ctx.SaveChangesAsync();

        var handler = new GetCurrenciesHandler(ctx, tenant);
        var result = await handler.Handle(new GetCurrenciesQuery(), CancellationToken.None);

        Assert.Single(result);
        Assert.Equal("USD", result[0].Code);
    }
}
