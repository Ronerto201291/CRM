using Erp.Modules.Accounting.Application.Queries;
using Erp.Modules.Accounting.Infrastructure.Data;
using Erp.Tests.TestSupport;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Erp.Tests.Accounting;

public class GetDiarioHandlerTests
{
    [Fact]
    public async Task Handle_WithNoEntries_ReturnsEmptyDiario()
    {
        var companyId = Guid.NewGuid();
        var tenant = new FakeTenantContext();
        tenant.SetTenant(companyId, "Empresa test");

        var options = new DbContextOptionsBuilder<AccountingDbContext>()
            .UseInMemoryDatabase($"accounting-diario-{Guid.NewGuid()}")
            .Options;

        await using var ctx = new AccountingDbContext(options, tenant);
        var handler = new GetDiarioHandler(ctx, tenant);
        var result = await handler.Handle(new GetDiarioQuery
        {
            FechaInicio = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc),
            FechaFin = new DateTime(2026, 12, 31, 0, 0, 0, DateTimeKind.Utc),
        }, CancellationToken.None);

        Assert.Empty(result.Lineas);
        Assert.Equal(0, result.TotalRegistros);
    }
}
