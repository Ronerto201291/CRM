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
        var handler = new ExportTc1Handler(ctx);
        var result = await handler.Handle(new ExportTc1Query(2026, 3), CancellationToken.None);

        Assert.Equal("text/csv", result.ContentType);
        Assert.Contains("TC1_RESUMEN_COTIZACION", System.Text.Encoding.UTF8.GetString(result.Content));
        Assert.Contains("2026-03", System.Text.Encoding.UTF8.GetString(result.Content));
    }
}
