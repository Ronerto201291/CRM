using Erp.Modules.Payroll.Application.Features.Exports;
using Erp.Modules.Payroll.Domain.Entities;
using Erp.Modules.Payroll.Infrastructure.Data;
using Erp.Tests.TestSupport;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Erp.Tests.Payroll;

public class ExportTc2HandlerTests
{
    [Fact]
    public async Task Handle_WithFinalLines_ReturnsCsvWithEmployeeData()
    {
        var companyId = Guid.NewGuid();
        var tenant = new FakeTenantContext();
        tenant.SetTenant(companyId, "Empresa test");

        var options = new DbContextOptionsBuilder<PayrollDbContext>()
            .UseInMemoryDatabase($"payroll-tc2-{Guid.NewGuid()}")
            .Options;

        await using var ctx = new PayrollDbContext(options, tenant);
        var employee = new Employee
        {
            Id = Guid.NewGuid(),
            CompanyId = companyId,
            TaxId = "12345678A",
            FullName = "Carlos Test",
            SocialSecurityNumber = "NAF-001",
            HireDate = DateTime.UtcNow.Date,
            ContractType = "Indefinido",
            WeeklyHours = 40,
        };
        var settlement = new PayrollSettlement
        {
            Id = Guid.NewGuid(),
            CompanyId = companyId,
            Year = 2026,
            Month = 5,
            Status = "Final",
            Lines =
            [
                new PayrollLine
                {
                    EmployeeId = employee.Id,
                    Employee = employee,
                    GrossSalary = 2800m,
                    IrpfBase = 2800m,
                    IrpfRate = 15m,
                    IrpfWithheld = 420m,
                    NetPay = 2200m,
                    CommonContingenciesBase = 2800m,
                    EmployeeSocialSecurity = 168m,
                    EmployerSocialSecurity = 672m,
                },
            ],
        };
        ctx.PayrollSettlements.Add(settlement);
        await ctx.SaveChangesAsync();

        var handler = new ExportTc2Handler(ctx);
        var result = await handler.Handle(new ExportTc2Query(2026, 5), CancellationToken.None);

        var text = System.Text.Encoding.UTF8.GetString(result.Content);
        Assert.Equal("text/csv", result.ContentType);
        Assert.Contains("TC2_RETENCIONES_Y_LIQUIDACION", text);
        Assert.Contains("12345678A", text);
        Assert.Contains("Carlos Test", text);
    }
}

public class ExportTcRedOrientativoHandlerTests
{
    [Fact]
    public async Task Handle_ReturnsXmlWithDisclaimer()
    {
        var companyId = Guid.NewGuid();
        var tenant = new FakeTenantContext();
        tenant.SetTenant(companyId, "Empresa test");

        var options = new DbContextOptionsBuilder<PayrollDbContext>()
            .UseInMemoryDatabase($"payroll-tcred-{Guid.NewGuid()}")
            .Options;

        await using var ctx = new PayrollDbContext(options, tenant);
        var handler = new ExportTcRedOrientativoHandler(ctx);
        var result = await handler.Handle(new ExportTcRedOrientativoQuery(2026, 8), CancellationToken.None);

        var text = System.Text.Encoding.UTF8.GetString(result.Content);
        Assert.Equal("application/xml", result.ContentType);
        Assert.Contains("RemisionCotizacionOrientativa", text);
        Assert.Contains("2026-08", text);
    }

    [Fact]
    public async Task Handle_Throws_ForInvalidMonth()
    {
        var tenant = new FakeTenantContext();
        tenant.SetTenant(Guid.NewGuid(), "Empresa test");

        var options = new DbContextOptionsBuilder<PayrollDbContext>()
            .UseInMemoryDatabase($"payroll-tc2-invalid-{Guid.NewGuid()}")
            .Options;

        await using var ctx = new PayrollDbContext(options, tenant);
        var handler = new ExportTc2Handler(ctx);

        await Assert.ThrowsAsync<ArgumentException>(() =>
            handler.Handle(new ExportTc2Query(2026, 13), CancellationToken.None));
    }
}
