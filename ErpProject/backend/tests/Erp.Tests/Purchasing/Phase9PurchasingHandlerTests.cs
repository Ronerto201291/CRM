using Erp.Modules.Purchasing.Application.Features.Orders;
using Erp.Modules.Purchasing.Domain.Entities;
using Erp.Modules.Purchasing.Infrastructure.Data;
using Erp.Tests.TestSupport;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Erp.Tests.Purchasing;

public class DeletePurchaseOrderHandlerTests
{
    [Fact]
    public async Task Handle_RemovesOrder_WhenExists()
    {
        var companyId = Guid.NewGuid();
        var orderId = Guid.NewGuid();
        var tenant = new FakeTenantContext();
        tenant.SetTenant(companyId, "Empresa test");

        var options = new DbContextOptionsBuilder<PurchasingDbContext>()
            .UseInMemoryDatabase($"purch-delete-{Guid.NewGuid()}")
            .Options;

        await using var ctx = new PurchasingDbContext(options, tenant);
        ctx.PurchaseOrders.Add(new PurchaseOrder
        {
            Id = orderId,
            CompanyId = companyId,
            Number = "PO-100",
            OrderDate = DateTime.UtcNow,
        });
        await ctx.SaveChangesAsync();

        var ok = await new DeletePurchaseOrderHandler(ctx, tenant).Handle(new DeletePurchaseOrderCommand(orderId), CancellationToken.None);
        Assert.True(ok);
        Assert.Empty(await ctx.PurchaseOrders.ToListAsync());
    }
}

public class UpdatePurchaseOrderHandlerTests
{
    [Fact]
    public async Task Handle_UpdatesOrderNumber()
    {
        var companyId = Guid.NewGuid();
        var orderId = Guid.NewGuid();
        var tenant = new FakeTenantContext();
        tenant.SetTenant(companyId, "Empresa test");

        var options = new DbContextOptionsBuilder<PurchasingDbContext>()
            .UseInMemoryDatabase($"purch-update-{Guid.NewGuid()}")
            .Options;

        await using var ctx = new PurchasingDbContext(options, tenant);
        ctx.PurchaseOrders.Add(new PurchaseOrder
        {
            Id = orderId,
            CompanyId = companyId,
            Number = "PO-OLD",
            OrderDate = DateTime.UtcNow,
            Lines = [new PurchaseOrderLine { Quantity = 1, UnitPrice = 50m }],
        });
        await ctx.SaveChangesAsync();

        var result = await new UpdatePurchaseOrderHandler(ctx, tenant).Handle(new UpdatePurchaseOrderCommand(
            orderId,
            "PO-NEW",
            DateTime.UtcNow,
            [new PurchaseOrderLineDto(null, 2m, 25m)]), CancellationToken.None);

        Assert.NotNull(result);
        Assert.Equal("PO-NEW", result!.Number);
    }
}
