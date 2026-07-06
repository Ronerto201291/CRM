using Erp.Application.Common.Events;
using Erp.Modules.Inventory.Application.EventHandlers;
using Erp.Modules.Inventory.Domain.Entities;
using Erp.Modules.Inventory.Infrastructure.Data;
using Erp.Tests.TestSupport;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Erp.Tests.Inventory;

public class GoodsReceiptInventoryHandlerTests
{
    [Fact]
    public async Task Handle_IncrementsStock_WhenWarehouseAndProductExist()
    {
        var companyId = Guid.NewGuid();
        var productId = Guid.NewGuid();
        var warehouseId = Guid.NewGuid();
        var receiptId = Guid.NewGuid();
        var tenant = new FakeTenantContext();
        tenant.SetTenant(companyId, "Empresa test");

        var options = new DbContextOptionsBuilder<InventoryDbContext>()
            .UseInMemoryDatabase($"gr-inv-{Guid.NewGuid()}")
            .Options;

        await using var ctx = new InventoryDbContext(options, tenant);
        ctx.Warehouses.Add(new Warehouse { Id = warehouseId, CompanyId = companyId, Name = "Central", IsActive = true });
        ctx.InventoryProducts.Add(new Product
        {
            Id = productId,
            CompanyId = companyId,
            Name = "Material",
            SKU = "MAT-1",
            CostPrice = 8m,
            TrackStock = true,
        });
        await ctx.SaveChangesAsync();

        var handler = new GoodsReceiptInventoryHandler(ctx, new FakePublisher(), NullLogger<GoodsReceiptInventoryHandler>.Instance);
        await handler.Handle(new GoodsReceiptCreatedEvent
        {
            GoodsReceiptId = receiptId,
            CompanyId = companyId,
            Lines =
            [
                new StockLineEventDto { ProductId = productId, Quantity = 10m, UnitCost = 8m },
            ],
        }, CancellationToken.None);

        var stock = await ctx.Stocks.SingleAsync(s => s.ProductId == productId);
        Assert.Equal(10m, stock.Quantity);
        Assert.Single(await ctx.StockMovements.Where(m => m.ReferenceId == receiptId).ToListAsync());
    }

    [Fact]
    public async Task Handle_IsIdempotent_WhenMovementsAlreadyExist()
    {
        var companyId = Guid.NewGuid();
        var productId = Guid.NewGuid();
        var warehouseId = Guid.NewGuid();
        var receiptId = Guid.NewGuid();
        var tenant = new FakeTenantContext();
        tenant.SetTenant(companyId, "Empresa test");

        var options = new DbContextOptionsBuilder<InventoryDbContext>()
            .UseInMemoryDatabase($"gr-idem-{Guid.NewGuid()}")
            .Options;

        await using var ctx = new InventoryDbContext(options, tenant);
        ctx.Warehouses.Add(new Warehouse { Id = warehouseId, CompanyId = companyId, Name = "Central", IsActive = true });
        ctx.InventoryProducts.Add(new Product { Id = productId, CompanyId = companyId, Name = "P", SKU = "P1", TrackStock = true });
        ctx.StockMovements.Add(new StockMovement
        {
            CompanyId = companyId,
            ProductId = productId,
            WarehouseId = warehouseId,
            ReferenceType = "GoodsReceipt",
            ReferenceId = receiptId,
            MovementType = "Purchase",
            Quantity = 5m,
            CreatedAt = DateTime.UtcNow,
        });
        await ctx.SaveChangesAsync();

        var handler = new GoodsReceiptInventoryHandler(ctx, new FakePublisher(), NullLogger<GoodsReceiptInventoryHandler>.Instance);
        var evt = new GoodsReceiptCreatedEvent
        {
            GoodsReceiptId = receiptId,
            CompanyId = companyId,
            Lines = [new StockLineEventDto { ProductId = productId, Quantity = 3m, UnitCost = 5m }],
        };

        await handler.Handle(evt, CancellationToken.None);

        Assert.Equal(1, await ctx.StockMovements.CountAsync(m => m.ReferenceId == receiptId));
    }
}
