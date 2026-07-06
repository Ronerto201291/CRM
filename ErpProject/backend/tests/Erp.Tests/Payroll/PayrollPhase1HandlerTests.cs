using Erp.Modules.Payroll.Application.Features.Calculation;
using Erp.Modules.Payroll.Application.Features.Templates;
using Erp.Modules.Payroll.Domain.Entities;
using Erp.Modules.Payroll.Infrastructure.Data;
using Erp.Tests.TestSupport;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Erp.Tests.Payroll;

public class PayrollPhase1HandlerTests
{
    [Fact]
    public async Task CreateTemplate_PersistsDefaultTemplate()
    {
        var companyId = Guid.NewGuid();
        var tenant = new FakeTenantContext();
        tenant.SetTenant(companyId, "Test");

        var options = new DbContextOptionsBuilder<PayrollDbContext>()
            .UseInMemoryDatabase($"payroll-template-{Guid.NewGuid()}")
            .Options;

        await using var ctx = new PayrollDbContext(options, tenant);
        var handler = new CreatePayrollTemplateHandler(ctx, tenant);

        var result = await handler.Handle(new CreatePayrollTemplateCommand(
            "Estándar", null, null, null, 6.35m, 30m, 15m, IsDefault: true), CancellationToken.None);

        Assert.NotEqual(Guid.Empty, result.Id);
        var stored = await ctx.PayrollTemplates.FirstAsync();
        Assert.Equal("Estándar", stored.Name);
        Assert.True(stored.IsDefault);
    }

    [Fact]
    public async Task CalculateLine_CreatesLineWithSsAndIrpfConcepts()
    {
        var companyId = Guid.NewGuid();
        var tenant = new FakeTenantContext();
        tenant.SetTenant(companyId, "Test");

        var options = new DbContextOptionsBuilder<PayrollDbContext>()
            .UseInMemoryDatabase($"payroll-calc-{Guid.NewGuid()}")
            .Options;

        await using var ctx = new PayrollDbContext(options, tenant);

        var emp = new Employee
        {
            Id = Guid.NewGuid(),
            CompanyId = companyId,
            TaxId = "12345678A",
            FullName = "Test",
            HireDate = DateTime.UtcNow.Date,
            IsActive = true,
        };
        var settlement = new PayrollSettlement
        {
            Id = Guid.NewGuid(),
            CompanyId = companyId,
            Year = 2026,
            Month = 6,
            Status = "Draft",
        };
        ctx.Employees.Add(emp);
        ctx.PayrollSettlements.Add(settlement);
        await ctx.SaveChangesAsync();

        var handler = new CalculatePayrollLineHandler(ctx);
        var result = await handler.Handle(new CalculatePayrollLineCommand(
            settlement.Id, emp.Id, 2000m, null, null), CancellationToken.None);

        Assert.Equal(2000m, result.GrossSalary);
        Assert.True(result.NetPay > 0);
        Assert.Contains("Orientativo", result.CalculationNote);

        var ssCount = await ctx.SocialSecurityContributions.CountAsync(c => c.PayrollLineId == result.LineId);
        var baseCount = await ctx.TaxableBases.CountAsync(b => b.PayrollLineId == result.LineId);
        Assert.Equal(1, ssCount);
        Assert.Equal(1, baseCount);
    }
}
