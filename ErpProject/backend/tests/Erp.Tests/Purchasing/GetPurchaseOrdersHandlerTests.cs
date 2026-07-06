using Erp.Modules.Purchasing.Application.Features.Orders;
using Erp.Modules.Purchasing.Infrastructure.Data;
using Erp.Tests.TestSupport;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Erp.Tests.Purchasing;

public class GetPurchaseOrdersHandlerTests
{
    [Fact]
    public async Task Handle_ReturnsEmptyList_WhenNoOrders()
    {
        var companyId = Guid.NewGuid();
        var tenant = new FakeTenantContext();
        tenant.SetTenant(companyId, "Empresa test");

        var options = new DbContextOptionsBuilder<PurchasingDbContext>()
            .UseInMemoryDatabase($"purchasing-orders-{Guid.NewGuid()}")
            .Options;

        await using var ctx = new PurchasingDbContext(options, tenant);
        var handler = new GetPurchaseOrdersHandler(ctx, tenant, new FakeSupplierInfoService());
        var result = await handler.Handle(new GetPurchaseOrdersQuery(), CancellationToken.None);

        Assert.Empty(result);
    }
}
