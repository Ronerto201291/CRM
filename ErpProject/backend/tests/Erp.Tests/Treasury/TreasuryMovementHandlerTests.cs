using Erp.Modules.Treasury.Application.Features.Treasury.Commands;
using Erp.Modules.Treasury.Application.Features.Treasury.Handlers;
using Erp.Modules.Treasury.Domain.Entities;
using Erp.Modules.Treasury.Infrastructure.Data;
using Erp.Tests.TestSupport;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Erp.Tests.Treasury;

public class TreasuryMovementHandlerTests
{
    private static TreasuryDbContext CreateContext(FakeTenantContext tenant)
    {
        var options = new DbContextOptionsBuilder<TreasuryDbContext>()
            .UseInMemoryDatabase($"treasury-mv-{Guid.NewGuid()}")
            .Options;
        return new TreasuryDbContext(options, tenant);
    }

    [Fact]
    public async Task ImportBankStatement_ParsesCsvLines()
    {
        var companyId = Guid.NewGuid();
        var accountId = Guid.NewGuid();
        var tenant = new FakeTenantContext();
        tenant.SetTenant(companyId, "Empresa test");

        await using var ctx = CreateContext(tenant);
        ctx.BankAccounts.Add(new BankAccount
        {
            Id = accountId, CompanyId = companyId,
            Name = "Cuenta", Iban = "ES0000000000000000000001",
            BankName = "Banco", IsActive = true, CreatedAt = DateTime.UtcNow,
        });
        await ctx.SaveChangesAsync();

        var csv = """
            date,amount,description,ref
            2026-07-01,1500.50,Ingreso cliente,REF-1
            2026-07-02,-200.00,Pago proveedor,REF-2
            """;

        var handler = new ImportBankStatementHandler(ctx, tenant);
        var movements = await handler.Handle(
            new ImportBankStatementCommand(accountId, csv), CancellationToken.None);

        Assert.Equal(2, movements.Count);
        Assert.Equal(1500.50m, movements[0].Amount);
        Assert.Equal(-200m, movements[1].Amount);
    }

    [Fact]
    public async Task CreateBankMovement_PersistsMovement()
    {
        var companyId = Guid.NewGuid();
        var accountId = Guid.NewGuid();
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

        var handler = new CreateBankMovementHandler(ctx, tenant);
        var dto = await handler.Handle(new CreateBankMovementCommand(
            accountId, DateTime.UtcNow, 500m, "Credit", "TRF-1", "Transferencia"), CancellationToken.None);

        Assert.Equal(500m, dto.Amount);
        Assert.Single(await ctx.BankMovements.ToListAsync());
    }

    [Fact]
    public async Task GetBankMovements_ReturnsPaginatedResults()
    {
        var companyId = Guid.NewGuid();
        var accountId = Guid.NewGuid();
        var tenant = new FakeTenantContext();
        tenant.SetTenant(companyId, "Empresa test");

        await using var ctx = CreateContext(tenant);
        for (var i = 0; i < 3; i++)
        {
            ctx.BankMovements.Add(new BankMovement
            {
                Id = Guid.NewGuid(), CompanyId = companyId, BankAccountId = accountId,
                Date = DateTime.UtcNow.AddDays(-i), Amount = 100m * (i + 1),
                Type = "Credit", Description = $"M{i}", Reference = $"R{i}",
                Origin = "Manual", IsReconciled = false, CreatedAt = DateTime.UtcNow,
            });
        }
        await ctx.SaveChangesAsync();

        var handler = new GetBankMovementsHandler(ctx);
        var page = await handler.Handle(
            new GetBankMovementsQuery(accountId, Page: 1, PageSize: 2), CancellationToken.None);

        Assert.Equal(3, page.TotalCount);
        Assert.Equal(2, page.Items.Count);
    }

    [Fact]
    public async Task CreateCashEffect_PersistsEffect()
    {
        var companyId = Guid.NewGuid();
        var tenant = new FakeTenantContext();
        tenant.SetTenant(companyId, "Empresa test");

        await using var ctx = CreateContext(tenant);
        var handler = new CreateCashEffectHandler(ctx, tenant);

        var dto = await handler.Handle(new CreateCashEffectCommand(
            null, "Cliente SA", "B12345674", "EF-001",
            DateTime.UtcNow, DateTime.UtcNow.AddDays(30), 1200m, null, "Efecto comercial"), CancellationToken.None);

        Assert.Equal(1200m, dto.Amount);
        Assert.Single(await ctx.CashEffects.ToListAsync());
    }

    [Fact]
    public async Task CreatePaymentOrder_PersistsDraftOrder()
    {
        var companyId = Guid.NewGuid();
        var tenant = new FakeTenantContext();
        tenant.SetTenant(companyId, "Empresa test");

        await using var ctx = CreateContext(tenant);
        var handler = new CreatePaymentOrderHandler(ctx, tenant);

        var dto = await handler.Handle(new CreatePaymentOrderCommand(
            "Supplier", "Proveedor SL", "B98765432", "ES0000000000000000000003",
            "Pago factura", 750m, DateTime.UtcNow.AddDays(7), null, null, null), CancellationToken.None);

        Assert.Equal("Draft", dto.Status);
        Assert.Equal(750m, dto.Amount);
    }
}
