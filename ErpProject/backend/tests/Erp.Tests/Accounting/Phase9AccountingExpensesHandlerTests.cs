using Erp.Modules.Accounting.Application.Features.Vat;
using Erp.Modules.Accounting.Infrastructure.Data;
using Erp.Tests.TestSupport;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Erp.Tests.Accounting;

public class CalculateVatHandlerTests
{
    [Fact]
    public async Task Handle_CalculatesStandardVat_AndPersistsTransaction()
    {
        var companyId = Guid.NewGuid();
        var tenant = new FakeTenantContext();
        tenant.SetTenant(companyId, "Empresa test");

        var options = new DbContextOptionsBuilder<AccountingDbContext>()
            .UseInMemoryDatabase($"acct-vat-{Guid.NewGuid()}")
            .Options;

        await using var ctx = new AccountingDbContext(options, tenant);
        var handler = new CalculateVatHandler(ctx, tenant);

        var result = await handler.Handle(new CalculateVatCommand
        {
            Amount = 100m,
            VatType = "Standard",
            HasRecargo = false,
        }, CancellationToken.None);

        Assert.Equal(0.21m, result.VatRate);
        Assert.Equal(21m, result.VatAmount);
        Assert.Equal(121m, result.Total);
        Assert.NotEqual(Guid.Empty, result.TransactionId);
        Assert.Single(await ctx.VatTransactions.ToListAsync());
    }

    [Fact]
    public async Task Handle_AppliesRecargo_WhenRequested()
    {
        var companyId = Guid.NewGuid();
        var tenant = new FakeTenantContext();
        tenant.SetTenant(companyId, "Empresa test");

        var options = new DbContextOptionsBuilder<AccountingDbContext>()
            .UseInMemoryDatabase($"acct-vat-recargo-{Guid.NewGuid()}")
            .Options;

        await using var ctx = new AccountingDbContext(options, tenant);
        var handler = new CalculateVatHandler(ctx, tenant);

        var result = await handler.Handle(new CalculateVatCommand
        {
            Amount = 100m,
            VatType = "Standard",
            HasRecargo = true,
        }, CancellationToken.None);

        Assert.Equal(5.2m, result.RecargoAmount);
        Assert.Equal(126.2m, result.Total);
    }
}
