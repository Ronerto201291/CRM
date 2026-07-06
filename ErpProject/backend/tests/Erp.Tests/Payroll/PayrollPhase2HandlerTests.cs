using Erp.Domain.Entities.Core;
using Erp.Modules.Payroll.Application.Features.Exports;
using Erp.Modules.Payroll.Application.Features.Pdf;
using Erp.Modules.Payroll.Application.Interfaces;
using Erp.Modules.Payroll.Domain.Entities;
using Erp.Modules.Payroll.Infrastructure.Data;
using Erp.Modules.Payroll.Infrastructure.Services;
using Erp.Tests.TestSupport;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Erp.Tests.Payroll;

public class PayrollPhase2HandlerTests
{
    [Fact]
    public async Task ExportModel111_ReturnsCsvWithDisclaimer()
    {
        var companyId = Guid.NewGuid();
        var tenant = new FakeTenantContext();
        tenant.SetTenant(companyId, "Empresa test");

        var options = new DbContextOptionsBuilder<PayrollDbContext>()
            .UseInMemoryDatabase($"payroll-111-{Guid.NewGuid()}")
            .Options;
        var appOptions = new DbContextOptionsBuilder<Erp.Infrastructure.Data.ErpDbContext>()
            .UseInMemoryDatabase($"core-111-{Guid.NewGuid()}")
            .Options;

        await using var ctx = new PayrollDbContext(options, tenant);
        await using var appCtx = new Erp.Infrastructure.Data.ErpDbContext(appOptions, tenant);
        appCtx.Companies.Add(new Company
        {
            Id = companyId,
            Name = "Empresa test SL",
            TaxId = "B12345674",
            Address = "Calle Test 1",
            CreatedAt = DateTime.UtcNow,
        });
        await appCtx.SaveChangesAsync();

        var settlement = new PayrollSettlement
        {
            Id = Guid.NewGuid(),
            CompanyId = companyId,
            Year = 2026,
            Month = 3,
            Status = "Final",
        };
        var employee = new Employee
        {
            Id = Guid.NewGuid(),
            CompanyId = companyId,
            TaxId = "12345678Z",
            FullName = "Ana Test",
        };
        ctx.PayrollSettlements.Add(settlement);
        ctx.Employees.Add(employee);
        ctx.PayrollLines.Add(new PayrollLine
        {
            Id = Guid.NewGuid(),
            PayrollSettlementId = settlement.Id,
            EmployeeId = employee.Id,
            GrossSalary = 2000m,
            IrpfBase = 2000m,
            IrpfRate = 15m,
            IrpfWithheld = 300m,
            NetPay = 1700m,
        });
        await ctx.SaveChangesAsync();

        var handler = new ExportModel111Handler(ctx, appCtx, tenant);
        var result = await handler.Handle(new ExportModel111Query(2026, 1), CancellationToken.None);

        Assert.Equal("text/csv", result.ContentType);
        var text = System.Text.Encoding.UTF8.GetString(result.Content);
        Assert.Contains("MODELO_111_ORIENTATIVO", text);
        Assert.Contains("12345678Z", text);
        Assert.Contains("AEAT", result.Disclaimer);
    }

    [Fact]
    public async Task GetPayrollLinePdf_ReturnsPdfBytes()
    {
        var companyId = Guid.NewGuid();
        var tenant = new FakeTenantContext();
        tenant.SetTenant(companyId, "Empresa test");

        var options = new DbContextOptionsBuilder<PayrollDbContext>()
            .UseInMemoryDatabase($"payroll-pdf-{Guid.NewGuid()}")
            .Options;
        var appOptions = new DbContextOptionsBuilder<Erp.Infrastructure.Data.ErpDbContext>()
            .UseInMemoryDatabase($"core-pdf-{Guid.NewGuid()}")
            .Options;

        await using var ctx = new PayrollDbContext(options, tenant);
        await using var appCtx = new Erp.Infrastructure.Data.ErpDbContext(appOptions, tenant);
        appCtx.Companies.Add(new Company
        {
            Id = companyId,
            Name = "Empresa PDF SL",
            TaxId = "B12345674",
            CreatedAt = DateTime.UtcNow,
        });
        await appCtx.SaveChangesAsync();

        var settlement = new PayrollSettlement
        {
            Id = Guid.NewGuid(),
            CompanyId = companyId,
            Year = 2026,
            Month = 6,
            Status = "Final",
        };
        var employee = new Employee
        {
            Id = Guid.NewGuid(),
            CompanyId = companyId,
            TaxId = "87654321X",
            FullName = "Carlos PDF",
        };
        var lineId = Guid.NewGuid();
        ctx.PayrollSettlements.Add(settlement);
        ctx.Employees.Add(employee);
        ctx.PayrollLines.Add(new PayrollLine
        {
            Id = lineId,
            PayrollSettlementId = settlement.Id,
            EmployeeId = employee.Id,
            GrossSalary = 1500m,
            CommonContingenciesBase = 1500m,
            EmployeeSocialSecurity = 100m,
            EmployerSocialSecurity = 450m,
            IrpfBase = 1500m,
            IrpfRate = 15m,
            IrpfWithheld = 225m,
            NetPay = 1175m,
        });
        await ctx.SaveChangesAsync();

        IPayrollPayslipPdfService pdfService = new PayrollPayslipPdfService();
        var handler = new GetPayrollLinePdfHandler(ctx, appCtx, tenant, pdfService);
        var result = await handler.Handle(new GetPayrollLinePdfQuery(lineId), CancellationToken.None);

        Assert.NotEmpty(result.PdfBytes);
        Assert.StartsWith("%PDF", System.Text.Encoding.ASCII.GetString(result.PdfBytes.AsSpan(0, 4)));
        Assert.Contains("87654321X", result.FileName);
    }
}
