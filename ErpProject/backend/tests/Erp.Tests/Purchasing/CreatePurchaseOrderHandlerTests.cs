using Erp.Modules.Purchasing.Application.Features.Orders;
using Erp.Modules.Purchasing.Infrastructure.Data;
using Erp.Tests.TestSupport;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Erp.Tests.Purchasing;

public class CreatePurchaseOrderHandlerTests
{
    [Fact]
    public async Task Handle_PersistsOrderWithLines()
    {
        var companyId = Guid.NewGuid();
        var tenant = new FakeTenantContext();
        tenant.SetTenant(companyId, "Empresa test");

        var options = new DbContextOptionsBuilder<PurchasingDbContext>()
            .UseInMemoryDatabase($"purchasing-create-{Guid.NewGuid()}")
            .Options;

        await using var ctx = new PurchasingDbContext(options, tenant);
        var handler = new CreatePurchaseOrderHandler(ctx, tenant);

        var orderDate = new DateTime(2026, 7, 1, 0, 0, 0, DateTimeKind.Utc);
        var result = await handler.Handle(new CreatePurchaseOrderCommand(
            "PO-2026-001",
            orderDate,
            [new PurchaseOrderLineDto(null, 10m, 25m)]), CancellationToken.None);

        Assert.Equal("PO-2026-001", result.Number);
        Assert.Single(result.Lines);
        Assert.Equal(10m, result.Lines[0].Quantity);

        var saved = await ctx.PurchaseOrders.Include(p => p.Lines).SingleAsync();
        Assert.Equal(companyId, saved.CompanyId);
    }
}

public class GetPurchaseOrderByIdHandlerTests
{
    [Fact]
    public async Task Handle_ReturnsOrder_WhenExists()
    {
        var companyId = Guid.NewGuid();
        var orderId = Guid.NewGuid();
        var tenant = new FakeTenantContext();
        tenant.SetTenant(companyId, "Empresa test");

        var options = new DbContextOptionsBuilder<PurchasingDbContext>()
            .UseInMemoryDatabase($"purchasing-byid-{Guid.NewGuid()}")
            .Options;

        await using var ctx = new PurchasingDbContext(options, tenant);
        ctx.PurchaseOrders.Add(new Erp.Modules.Purchasing.Domain.Entities.PurchaseOrder
        {
            Id = orderId,
            CompanyId = companyId,
            Number = "PO-99",
            OrderDate = DateTime.UtcNow,
        });
        await ctx.SaveChangesAsync();

        var handler = new GetPurchaseOrderByIdHandler(ctx, tenant);
        var result = await handler.Handle(new GetPurchaseOrderByIdQuery(orderId), CancellationToken.None);

        Assert.NotNull(result);
        Assert.Equal("PO-99", result!.Number);
    }
}
