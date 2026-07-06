using Erp.Modules.Payroll.Application.Features.Settlements;
using Erp.Modules.Payroll.Domain.Entities;
using Erp.Modules.Payroll.Infrastructure.Data;
using Erp.Tests.TestSupport;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Erp.Tests.Payroll;

public class GetSettlementsHandlerTests
{
    [Fact]
    public async Task Handle_ReturnsSettlementsForYear()
    {
        var companyId = Guid.NewGuid();
        var tenant = new FakeTenantContext();
        tenant.SetTenant(companyId, "Empresa test");

        var options = new DbContextOptionsBuilder<PayrollDbContext>()
            .UseInMemoryDatabase($"payroll-settlements-{Guid.NewGuid()}")
            .Options;

        await using var ctx = new PayrollDbContext(options, tenant);
        var settlement = new PayrollSettlement
        {
            Id = Guid.NewGuid(),
            CompanyId = companyId,
            Year = 2026,
            Month = 3,
            Status = "Draft",
            Lines =
            [
                new PayrollLine
                {
                    GrossSalary = 2000m,
                    IrpfWithheld = 300m,
                    EmployerSocialSecurity = 600m,
                },
            ],
        };
        ctx.PayrollSettlements.Add(settlement);
        await ctx.SaveChangesAsync();

        var handler = new GetSettlementsHandler(ctx);
        var result = await handler.Handle(new GetSettlementsQuery(2026), CancellationToken.None);

        Assert.Single(result);
        Assert.Equal(3, result[0].Month);
        Assert.Equal(2000m, result[0].TotalGross);
    }
}

public class CreateSettlementHandlerTests
{
    [Fact]
    public async Task Handle_CreatesDraftSettlement()
    {
        var companyId = Guid.NewGuid();
        var tenant = new FakeTenantContext();
        tenant.SetTenant(companyId, "Empresa test");

        var options = new DbContextOptionsBuilder<PayrollDbContext>()
            .UseInMemoryDatabase($"payroll-create-settlement-{Guid.NewGuid()}")
            .Options;

        await using var ctx = new PayrollDbContext(options, tenant);
        var handler = new CreateSettlementHandler(ctx, tenant);
        var result = await handler.Handle(new CreateSettlementCommand(2026, 4), CancellationToken.None);

        Assert.NotEqual(Guid.Empty, result.Id);
        var stored = await ctx.PayrollSettlements.SingleAsync();
        Assert.Equal("Draft", stored.Status);
        Assert.Equal(4, stored.Month);
    }

    [Fact]
    public async Task Handle_Throws_WhenDuplicateMonth()
    {
        var companyId = Guid.NewGuid();
        var tenant = new FakeTenantContext();
        tenant.SetTenant(companyId, "Empresa test");

        var options = new DbContextOptionsBuilder<PayrollDbContext>()
            .UseInMemoryDatabase($"payroll-dup-settlement-{Guid.NewGuid()}")
            .Options;

        await using var ctx = new PayrollDbContext(options, tenant);
        ctx.PayrollSettlements.Add(new PayrollSettlement
        {
            CompanyId = companyId,
            Year = 2026,
            Month = 5,
            Status = "Draft",
        });
        await ctx.SaveChangesAsync();

        var handler = new CreateSettlementHandler(ctx, tenant);
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            handler.Handle(new CreateSettlementCommand(2026, 5), CancellationToken.None));
    }
}

public class AddPayrollLineHandlerTests
{
    [Fact]
    public async Task Handle_AddsLineToDraftSettlement()
    {
        var companyId = Guid.NewGuid();
        var settlementId = Guid.NewGuid();
        var employeeId = Guid.NewGuid();
        var tenant = new FakeTenantContext();
        tenant.SetTenant(companyId, "Empresa test");

        var options = new DbContextOptionsBuilder<PayrollDbContext>()
            .UseInMemoryDatabase($"payroll-add-line-{Guid.NewGuid()}")
            .Options;

        await using var ctx = new PayrollDbContext(options, tenant);
        ctx.Employees.Add(new Employee
        {
            Id = employeeId,
            CompanyId = companyId,
            TaxId = "12345678A",
            FullName = "Ana",
            HireDate = DateTime.UtcNow.Date,
            ContractType = "Indefinido",
            WeeklyHours = 40,
        });
        ctx.PayrollSettlements.Add(new PayrollSettlement
        {
            Id = settlementId,
            CompanyId = companyId,
            Year = 2026,
            Month = 6,
            Status = "Draft",
        });
        await ctx.SaveChangesAsync();

        var handler = new AddPayrollLineHandler(ctx);
        var result = await handler.Handle(new AddPayrollLineCommand(
            settlementId, employeeId, 2500m, 2500m, 150m, 600m, 2500m, 15m, 375m, 1975m), CancellationToken.None);

        Assert.NotEqual(Guid.Empty, result.LineId);
        var line = await ctx.PayrollLines.SingleAsync();
        Assert.Equal(2500m, line.GrossSalary);
    }
}

public class FinalizeSettlementHandlerTests
{
    [Fact]
    public async Task Handle_FinalizesSettlement_AndGeneratesJournalEntry()
    {
        var companyId = Guid.NewGuid();
        var settlementId = Guid.NewGuid();
        var employeeId = Guid.NewGuid();
        var journalId = Guid.NewGuid();
        var tenant = new FakeTenantContext();
        tenant.SetTenant(companyId, "Empresa test");

        var options = new DbContextOptionsBuilder<PayrollDbContext>()
            .UseInMemoryDatabase($"payroll-finalize-{Guid.NewGuid()}")
            .Options;

        await using var ctx = new PayrollDbContext(options, tenant);
        ctx.PayrollSettlements.Add(new PayrollSettlement
        {
            Id = settlementId,
            CompanyId = companyId,
            Year = 2026,
            Month = 7,
            Status = "Draft",
            Lines =
            [
                new PayrollLine
                {
                    EmployeeId = employeeId,
                    GrossSalary = 3000m,
                    EmployeeSocialSecurity = 180m,
                    EmployerSocialSecurity = 720m,
                    IrpfWithheld = 450m,
                    NetPay = 2370m,
                },
            ],
        });
        await ctx.SaveChangesAsync();

        var journal = new FakePayrollJournalEntryGenerator { NextEntryId = journalId };
        var handler = new FinalizeSettlementHandler(ctx, tenant, journal);
        var result = await handler.Handle(new FinalizeSettlementCommand(settlementId), CancellationToken.None);

        Assert.False(result.AlreadyFinalized);
        Assert.Equal(journalId, result.JournalEntryId);
        var settlement = await ctx.PayrollSettlements.SingleAsync();
        Assert.Equal("Final", settlement.Status);
        Assert.Equal(journalId, settlement.JournalEntryId);
    }
}
