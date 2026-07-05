using Erp.Domain.Entities.Core;
using Erp.Modules.Payroll.Application.Features.Exports;
using Erp.Modules.Payroll.Infrastructure.Data;
using Erp.Tests.TestSupport;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Erp.Tests.Payroll;

public class ExportTc1HandlerTests
{
    [Fact]
    public async Task Handle_WithNoPayrollLines_ReturnsCsvWithHeader()
    {
        var companyId = Guid.NewGuid();
        var tenant = new FakeTenantContext();
        tenant.SetTenant(companyId, "Empresa test");

        var options = new DbContextOptionsBuilder<PayrollDbContext>()
            .UseInMemoryDatabase($"payroll-tc1-{Guid.NewGuid()}")
            .Options;

        await using var ctx = new PayrollDbContext(options, tenant);
        var appOptions = new DbContextOptionsBuilder<Erp.Infrastructure.Data.ErpDbContext>()
            .UseInMemoryDatabase($"core-tc1-{Guid.NewGuid()}")
            .Options;
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

        var handler = new ExportTc1Handler(ctx, appCtx, tenant);
        var result = await handler.Handle(new ExportTc1Query(2026, 3), CancellationToken.None);

        Assert.Equal("text/csv", result.ContentType);
        var text = System.Text.Encoding.UTF8.GetString(result.Content);
        Assert.Contains("TC1_RESUMEN_COTIZACION", text);
        Assert.Contains("2026-03", text);
        Assert.Contains("EmpresaNIF;B12345674", text);
        Assert.Contains("CCC_orientativo", text);
    }
}
