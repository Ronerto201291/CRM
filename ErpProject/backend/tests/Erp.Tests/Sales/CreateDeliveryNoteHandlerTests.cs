using Erp.Modules.Sales.Application.Features.Deliveries.Commands;
using Erp.Modules.Sales.Application.Features.Deliveries.Handlers;
using Erp.Modules.Sales.Application.Features.Orders;
using Erp.Modules.Sales.Domain.Entities;
using Erp.Modules.Sales.Infrastructure.Data;
using Erp.Tests.TestSupport;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Erp.Tests.Sales;

public class CreateDeliveryNoteHandlerTests
{
    [Fact]
    public async Task Handle_CreatesDeliveryNote_AndUpdatesOrderStatus()
    {
        var companyId = Guid.NewGuid();
        var orderId = Guid.NewGuid();
        var lineId = Guid.NewGuid();
        var tenant = new FakeTenantContext();
        tenant.SetTenant(companyId, "Empresa test");

        var options = new DbContextOptionsBuilder<SalesDbContext>()
            .UseInMemoryDatabase($"sales-delivery-create-{Guid.NewGuid()}")
            .Options;

        await using var ctx = new SalesDbContext(options, tenant);
        var order = new SalesOrder
        {
            Id = orderId,
            CompanyId = companyId,
            Number = "PED-100",
            OrderDate = DateTime.UtcNow,
            ClientName = "Cliente",
            Status = "Open",
            Lines =
            [
                new SalesOrderLine
                {
                    Id = lineId,
                    SalesOrderId = orderId,
                    Quantity = 10m,
                    UnitPrice = 25m,
                    DeliveredQuantity = 0m,
                },
            ],
        };
        ctx.SalesOrders.Add(order);
        await ctx.SaveChangesAsync();

        var handler = new CreateDeliveryNoteHandler(ctx, new FakePublisher());
        var noteId = await handler.Handle(new CreateDeliveryNoteCommand
        {
            SalesOrderId = orderId,
            Number = "ALB-100",
            DeliveryDate = DateTime.UtcNow,
            Lines =
            [
                new CreateDeliveryNoteLineDto
                {
                    SalesOrderLineId = lineId,
                    ShippedQuantity = 10m,
                },
            ],
        }, CancellationToken.None);

        Assert.NotEqual(Guid.Empty, noteId);
        var updatedOrder = await ctx.SalesOrders.Include(s => s.Lines).SingleAsync();
        Assert.Equal("Completed", updatedOrder.Status);
        Assert.Equal(10m, updatedOrder.Lines.Single().DeliveredQuantity);

        var note = await ctx.DeliveryNotes.Include(d => d.Lines).SingleAsync();
        Assert.Equal("ALB-100", note.Number);
    }

    [Fact]
    public async Task Handle_Throws_WhenSalesOrderNotFound()
    {
        var tenant = new FakeTenantContext();
        tenant.SetTenant(Guid.NewGuid(), "Empresa test");

        var options = new DbContextOptionsBuilder<SalesDbContext>()
            .UseInMemoryDatabase($"sales-delivery-miss-{Guid.NewGuid()}")
            .Options;

        await using var ctx = new SalesDbContext(options, tenant);
        var handler = new CreateDeliveryNoteHandler(ctx, new FakePublisher());

        await Assert.ThrowsAsync<InvalidOperationException>(() => handler.Handle(
            new CreateDeliveryNoteCommand { SalesOrderId = Guid.NewGuid(), Number = "ALB-X" },
            CancellationToken.None));
    }
}

public class GetSalesOrdersHandlerTests
{
    [Fact]
    public async Task Handle_ReturnsOrdersForTenant()
    {
        var companyId = Guid.NewGuid();
        var tenant = new FakeTenantContext();
        tenant.SetTenant(companyId, "Empresa test");

        var options = new DbContextOptionsBuilder<SalesDbContext>()
            .UseInMemoryDatabase($"sales-orders-list-{Guid.NewGuid()}")
            .Options;

        await using var ctx = new SalesDbContext(options, tenant);
        ctx.SalesOrders.Add(new SalesOrder
        {
            CompanyId = companyId,
            Number = "PED-200",
            OrderDate = DateTime.UtcNow,
            ClientName = "Cliente list",
            SubTotal = 100m,
            TaxAmount = 21m,
            Total = 121m,
        });
        await ctx.SaveChangesAsync();

        var handler = new GetSalesOrdersHandler(ctx, tenant);
        var result = await handler.Handle(new GetSalesOrdersQuery(), CancellationToken.None);

        Assert.Single(result);
        Assert.Equal("PED-200", result[0].Number);
    }

    [Fact]
    public async Task GetSalesOrderById_ReturnsNull_WhenNotFound()
    {
        var tenant = new FakeTenantContext();
        tenant.SetTenant(Guid.NewGuid(), "Empresa test");

        var options = new DbContextOptionsBuilder<SalesDbContext>()
            .UseInMemoryDatabase($"sales-order-byid-{Guid.NewGuid()}")
            .Options;

        await using var ctx = new SalesDbContext(options, tenant);
        var handler = new GetSalesOrderByIdHandler(ctx, tenant);
        var result = await handler.Handle(new GetSalesOrderByIdQuery(Guid.NewGuid()), CancellationToken.None);

        Assert.Null(result);
    }
}
