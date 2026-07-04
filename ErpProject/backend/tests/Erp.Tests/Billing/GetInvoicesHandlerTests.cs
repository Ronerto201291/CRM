using Erp.Modules.Billing.Application.Features.Billing.Handlers;
using Erp.Modules.Billing.Application.Features.Billing.Queries;
using Erp.Modules.Billing.Infrastructure.Data;
using Erp.Tests.TestSupport;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Erp.Tests.Billing;

public class GetInvoicesHandlerTests
{
    [Fact]
    public async Task Handle_ReturnsEmptyPaginatedResult_WhenNoInvoices()
    {
        var companyId = Guid.NewGuid();
        var tenant = new FakeTenantContext();
        tenant.SetTenant(companyId, "Empresa test");

        var options = new DbContextOptionsBuilder<BillingDbContext>()
            .UseInMemoryDatabase($"billing-invoices-{Guid.NewGuid()}")
            .Options;

        await using var ctx = new BillingDbContext(options, tenant);
        var handler = new GetInvoicesHandler(ctx);
        var result = await handler.Handle(new GetInvoicesQuery(), CancellationToken.None);

        Assert.Empty(result.Items);
        Assert.Equal(0, result.TotalCount);
    }
}
