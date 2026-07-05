using Erp.Application.Common.Interfaces;
using Erp.Domain.Entities.Core;
using Erp.Modules.Payroll.Application.Features.Exports;
using Erp.Modules.Payroll.Domain.Entities;
using Erp.Modules.Payroll.Infrastructure.Data;
using Erp.Tests.TestSupport;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Erp.Tests.Payroll;

public class ExportRedHandlerTests
{
    [Fact]
    public async Task Handle_GeneratesFixedLengthRedFile_MatchingGoldenStructure()
    {
        var companyId = Guid.NewGuid();
        var tenant = new FakeTenantContext();
        tenant.SetTenant(companyId, "Empresa RED");

        var payrollOptions = new DbContextOptionsBuilder<PayrollDbContext>()
            .UseInMemoryDatabase($"payroll-red-{Guid.NewGuid()}")
            .Options;
        await using var payrollCtx = new PayrollDbContext(payrollOptions, tenant);

        var employeeId = Guid.NewGuid();
        var settlementId = Guid.NewGuid();
        payrollCtx.Employees.Add(new Employee
        {
            Id = employeeId,
            CompanyId = companyId,
            TaxId = "12345678Z",
            FullName = "Ana Test",
            SocialSecurityNumber = "281234567890",
            CreatedAt = DateTime.UtcNow,
        });
        payrollCtx.PayrollSettlements.Add(new PayrollSettlement
        {
            Id = settlementId,
            CompanyId = companyId,
            Year = 2026,
            Month = 3,
            Status = "Final",
            CreatedAt = DateTime.UtcNow,
        });
        payrollCtx.PayrollLines.Add(new PayrollLine
        {
            Id = Guid.NewGuid(),
            PayrollSettlementId = settlementId,
            EmployeeId = employeeId,
            GrossSalary = 2500m,
            CommonContingenciesBase = 2500m,
            EmployeeSocialSecurity = 157.50m,
            EmployerSocialSecurity = 650m,
            IrpfBase = 2500m,
            IrpfRate = 15m,
            IrpfWithheld = 375m,
            NetPay = 1967.50m,
            CreatedAt = DateTime.UtcNow,
        });
        await payrollCtx.SaveChangesAsync();

        var appOptions = new DbContextOptionsBuilder<Erp.Infrastructure.Data.ErpDbContext>()
            .UseInMemoryDatabase($"core-red-{Guid.NewGuid()}")
            .Options;
        await using var appCtx = new Erp.Infrastructure.Data.ErpDbContext(appOptions, tenant);
        appCtx.Companies.Add(new Company
        {
            Id = companyId,
            Name = "Empresa RED SL",
            TaxId = "B12345674",
            Address = "Calle Test 1",
            CreatedAt = DateTime.UtcNow,
        });
        await appCtx.SaveChangesAsync();

        var handler = new ExportRedHandler(payrollCtx, appCtx, tenant);
        var result = await handler.Handle(new ExportRedQuery(2026, 3), CancellationToken.None);

        var text = System.Text.Encoding.GetEncoding("ISO-8859-1").GetString(result.Content);
        var lines = text.Split('\n', StringSplitOptions.RemoveEmptyEntries);
        Assert.Equal(3, lines.Length);
        Assert.All(lines, l => Assert.Equal(250, l.TrimEnd('\r').Length));
        Assert.StartsWith("01", lines[0]);
        Assert.StartsWith("02", lines[1]);
        Assert.StartsWith("99", lines[2]);
        Assert.Contains("202603", lines[0]);
        Assert.Contains("281234567890", lines[1]);
    }
}
