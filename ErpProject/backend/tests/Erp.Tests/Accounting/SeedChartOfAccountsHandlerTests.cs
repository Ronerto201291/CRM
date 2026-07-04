using Erp.Application.Common.Events;
using Erp.Modules.Accounting.Application.Handlers;
using Erp.Modules.Accounting.Infrastructure.Data;
using Erp.Tests.TestSupport;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Erp.Tests.Accounting;

public class SeedChartOfAccountsHandlerTests
{
    private static AccountingDbContext NewContext(FakeTenantContext tenant)
    {
        var options = new DbContextOptionsBuilder<AccountingDbContext>()
            .UseInMemoryDatabase($"seed-pgc-{Guid.NewGuid()}")
            .Options;
        return new AccountingDbContext(options, tenant);
    }

    [Fact]
    public async Task Handle_SeedsCoreAccountsIncludingCashBankTpvBizum()
    {
        var companyId = Guid.NewGuid();
        var tenant = new FakeTenantContext();
        tenant.SetTenant(companyId, "Empresa Test");
        await using var ctx = NewContext(tenant);

        var handler = new SeedChartOfAccountsHandler(ctx, NullLogger<SeedChartOfAccountsHandler>.Instance);
        await handler.Handle(new CompanyCreatedEvent { CompanyId = companyId }, CancellationToken.None);

        var accounts = await ctx.Accounts.ToListAsync();
        Assert.NotEmpty(accounts);
        Assert.All(accounts, a => Assert.Equal(companyId, a.CompanyId));

        var byCode = accounts.ToDictionary(a => a.Code);
        Assert.Equal("Asset", byCode["570"].Type);   // Caja
        Assert.Equal("Asset", byCode["572"].Type);   // Bancos
        Assert.Equal("Asset", byCode["430"].Type);   // Clientes
        Assert.Equal("Liability", byCode["400"].Type); // Proveedores
        Assert.Equal("Income", byCode["705"].Type);

        // ADR-0018 #42b — cuentas de liquidación TPV/Bizum, distintas de "572"
        Assert.Equal("Asset", byCode["5721"].Type);
        Assert.Equal("Asset", byCode["5722"].Type);
    }

    [Fact]
    public async Task Handle_WhenAccountsAlreadyExist_IsIdempotent()
    {
        var companyId = Guid.NewGuid();
        var tenant = new FakeTenantContext();
        tenant.SetTenant(companyId, "Empresa Test");
        await using var ctx = NewContext(tenant);

        var handler = new SeedChartOfAccountsHandler(ctx, NullLogger<SeedChartOfAccountsHandler>.Instance);
        await handler.Handle(new CompanyCreatedEvent { CompanyId = companyId }, CancellationToken.None);
        var countAfterFirst = await ctx.Accounts.CountAsync();

        await handler.Handle(new CompanyCreatedEvent { CompanyId = companyId }, CancellationToken.None);
        var countAfterSecond = await ctx.Accounts.CountAsync();

        Assert.Equal(countAfterFirst, countAfterSecond);
    }

    [Fact]
    public async Task Handle_DoesNotAffectOtherCompanies()
    {
        var companyA = Guid.NewGuid();
        var companyB = Guid.NewGuid();
        var tenant = new FakeTenantContext();
        tenant.SetTenant(companyA, "Empresa A");
        await using var ctx = NewContext(tenant);

        var handler = new SeedChartOfAccountsHandler(ctx, NullLogger<SeedChartOfAccountsHandler>.Instance);
        await handler.Handle(new CompanyCreatedEvent { CompanyId = companyA }, CancellationToken.None);
        await handler.Handle(new CompanyCreatedEvent { CompanyId = companyB }, CancellationToken.None);

        var countA = await ctx.Accounts.IgnoreQueryFilters().CountAsync(a => a.CompanyId == companyA);
        var countB = await ctx.Accounts.IgnoreQueryFilters().CountAsync(a => a.CompanyId == companyB);
        Assert.Equal(countA, countB);
        Assert.True(countA > 0);
    }
}
