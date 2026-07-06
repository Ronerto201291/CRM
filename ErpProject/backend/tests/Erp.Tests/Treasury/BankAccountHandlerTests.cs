using Erp.Modules.Treasury.Application.Features.Treasury.Commands;
using Erp.Modules.Treasury.Application.Features.Treasury.Handlers;
using Erp.Modules.Treasury.Domain.Entities;
using Erp.Modules.Treasury.Infrastructure.Data;
using Erp.Tests.TestSupport;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Erp.Tests.Treasury;

public class BankAccountHandlerTests
{
    [Fact]
    public async Task CreateBankAccount_PersistsAccount()
    {
        var companyId = Guid.NewGuid();
        var tenant = new FakeTenantContext();
        tenant.SetTenant(companyId, "Empresa test");

        await using var ctx = CreateContext(tenant);
        var handler = new CreateBankAccountHandler(ctx, tenant);

        var result = await handler.Handle(
            new CreateBankAccountCommand("Cuenta principal", "ES9121000418450200051332", "CAIXESBBXXX", "CaixaBank", "572"),
            CancellationToken.None);

        Assert.Equal("Cuenta principal", result.Name);
        Assert.Equal("ES9121000418450200051332", result.Iban);
        Assert.Single(await ctx.BankAccounts.ToListAsync());
    }

    [Fact]
    public async Task GetBankAccounts_ReturnsTenantAccounts()
    {
        var companyId = Guid.NewGuid();
        var tenant = new FakeTenantContext();
        tenant.SetTenant(companyId, "Empresa test");

        await using var ctx = CreateContext(tenant);
        ctx.BankAccounts.Add(new BankAccount
        {
            Id = Guid.NewGuid(), CompanyId = companyId,
            Name = "Cuenta 1", Iban = "ES0000000000000000000001",
            BankName = "Banco", CurrentBalance = 1000m, IsActive = true,
            CreatedAt = DateTime.UtcNow,
        });
        await ctx.SaveChangesAsync();

        var handler = new GetBankAccountsHandler(ctx, tenant);
        var accounts = await handler.Handle(new GetBankAccountsQuery(), CancellationToken.None);

        Assert.Single(accounts);
        Assert.Equal("Cuenta 1", accounts[0].Name);
        Assert.Equal(0m, accounts[0].CurrentBalance);
    }

    [Fact]
    public async Task GetBankAccountById_ReturnsAccount()
    {
        var accountId = Guid.NewGuid();
        var companyId = Guid.NewGuid();
        var tenant = new FakeTenantContext();
        tenant.SetTenant(companyId, "Empresa test");

        await using var ctx = CreateContext(tenant);
        ctx.BankAccounts.Add(new BankAccount
        {
            Id = accountId, CompanyId = companyId,
            Name = "Cuenta", Iban = "ES0000000000000000000002",
            BankName = "Banco", IsActive = true, CreatedAt = DateTime.UtcNow,
        });
        await ctx.SaveChangesAsync();

        var handler = new GetBankAccountHandler(ctx, tenant);
        var dto = await handler.Handle(new GetBankAccountQuery(accountId), CancellationToken.None);

        Assert.NotNull(dto);
        Assert.Equal("Cuenta", dto!.Name);
    }

    private static TreasuryDbContext CreateContext(FakeTenantContext tenant)
    {
        var options = new DbContextOptionsBuilder<TreasuryDbContext>()
            .UseInMemoryDatabase($"bank-acct-{Guid.NewGuid()}")
            .Options;
        return new TreasuryDbContext(options, tenant);
    }
}
