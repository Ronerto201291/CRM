using Erp.Application.Common.Interfaces;
using Erp.Modules.Treasury.Application.Features.Treasury.Handlers;
using Erp.Modules.Treasury.Domain.Entities;
using Erp.Modules.Treasury.Infrastructure.Data;
using Erp.Tests.TestSupport;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Erp.Tests.Treasury;

/// <summary>
/// Cubre el bug real corregido tras la contra-auditoría jul 2026 (ADR-0018 ítem del catálogo):
/// el outflow recurrente siempre devolvía 0 porque salía de un stub en
/// Crm.Infrastructure.AutomationRecurringQuery.GetMonthlyRecurringOutflowAsync. Ahora sale de
/// IAutomationPayrollQuery (Payroll), un puerto real separado — este test falla si alguien
/// vuelve a fusionar outflow dentro de IAutomationRecurringQuery o si el handler deja de
/// consumir IAutomationPayrollQuery.
/// </summary>
public class TreasuryLiquidityForecastHandlerTests
{
    [Fact]
    public async Task Handle_IncludesRealPayrollOutflow_NotZero()
    {
        var companyId = Guid.NewGuid();
        var tenant = new FakeTenantContext();
        tenant.SetTenant(companyId, "Empresa test");

        await using var ctx = CreateContext(tenant);
        ctx.BankAccounts.Add(new BankAccount
        {
            Id = Guid.NewGuid(), CompanyId = companyId, Name = "Cuenta", Iban = "ES00",
            BankName = "Banco", IsActive = true, CurrentBalance = 10000m, CreatedAt = DateTime.UtcNow,
        });
        await ctx.SaveChangesAsync();

        var billing = new FakeAutomationBillingQuery();
        var expenses = new FakeAutomationExpensesQuery();
        var recurring = new FakeAutomationRecurringQuery { MonthlyInflow = 500m };
        var payroll = new FakeAutomationPayrollQuery { MonthlyPayrollCost = 3000m };

        var handler = new GetTreasuryLiquidityForecastHandler(ctx, tenant, billing, expenses, recurring, payroll);

        var result = await handler.Handle(new GetTreasuryLiquidityForecastQuery(), CancellationToken.None);

        Assert.Equal(10000m, result.CurrentBankBalance);
        var horizon30 = Assert.Single(result.Horizons.Where(h => h.Days == 30));
        Assert.Equal(500m, horizon30.RecurringInflow);
        // Antes del fix esto era siempre 0 sin importar el coste real de nómina.
        Assert.Equal(3000m, horizon30.RecurringOutflow);
        Assert.Equal(10000m + 500m - 3000m, horizon30.ProjectedBalance);
    }

    [Fact]
    public async Task Handle_NoFinalPayrollSettlement_OutflowIsZero_NotAnException()
    {
        var companyId = Guid.NewGuid();
        var tenant = new FakeTenantContext();
        tenant.SetTenant(companyId, "Empresa nueva");

        await using var ctx = CreateContext(tenant);

        var handler = new GetTreasuryLiquidityForecastHandler(
            ctx, tenant,
            new FakeAutomationBillingQuery(),
            new FakeAutomationExpensesQuery(),
            new FakeAutomationRecurringQuery(),
            new FakeAutomationPayrollQuery { MonthlyPayrollCost = 0m });

        var result = await handler.Handle(new GetTreasuryLiquidityForecastQuery(), CancellationToken.None);

        Assert.All(result.Horizons, h => Assert.Equal(0m, h.RecurringOutflow));
    }

    private static TreasuryDbContext CreateContext(FakeTenantContext tenant)
    {
        var options = new DbContextOptionsBuilder<TreasuryDbContext>()
            .UseInMemoryDatabase($"liquidity-forecast-{Guid.NewGuid()}")
            .Options;
        return new TreasuryDbContext(options, tenant);
    }
}
