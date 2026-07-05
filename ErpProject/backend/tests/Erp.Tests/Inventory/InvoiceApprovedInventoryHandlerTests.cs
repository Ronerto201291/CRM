using Erp.Application.Common.Events;
using Erp.Modules.Inventory.Application.EventHandlers;
using Erp.Modules.Inventory.Domain.Entities;
using Erp.Modules.Inventory.Infrastructure.Data;
using Erp.Tests.TestSupport;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Erp.Tests.Inventory;

public class InvoiceApprovedInventoryHandlerTests
{
    [Fact]
    public async Task Handle_SkipsStockDecrement_WhenSalesOrderIdPresent()
    {
        var companyId = Guid.NewGuid();
        var productId = Guid.NewGuid();
        var warehouseId = Guid.NewGuid();
        var invoiceId = Guid.NewGuid();
        var tenant = new FakeTenantContext();
        tenant.SetTenant(companyId, "Empresa test");

        var options = new DbContextOptionsBuilder<InventoryDbContext>()
            .UseInMemoryDatabase($"inv-inv-skip-{Guid.NewGuid()}")
            .Options;

        await using var ctx = new InventoryDbContext(options, tenant);
        ctx.Warehouses.Add(new Warehouse { Id = warehouseId, CompanyId = companyId, Name = "Central", IsActive = true });
        ctx.InventoryProducts.Add(new Product
        {
            Id = productId,
            CompanyId = companyId,
            Name = "Producto",
            SKU = "SKU-1",
            CostPrice = 10m,
            TrackStock = true,
        });
        ctx.Stocks.Add(new Stock { CompanyId = companyId, ProductId = productId, WarehouseId = warehouseId, Quantity = 100m });
        await ctx.SaveChangesAsync();

        var handler = new InvoiceApprovedInventoryHandler(ctx, new FakePublisher(), NullLogger<InvoiceApprovedInventoryHandler>.Instance);
        await handler.Handle(new InvoiceApprovedEvent
        {
            InvoiceId = invoiceId,
            CompanyId = companyId,
            SalesOrderId = Guid.NewGuid(),
            Lines =
            [
                new InvoiceLineEventDto { ProductId = productId, Quantity = 5m, UnitPrice = 10m },
            ],
        }, CancellationToken.None);

        Assert.Equal(100m, (await ctx.Stocks.SingleAsync()).Quantity);
        Assert.Empty(await ctx.StockMovements.Where(m => m.ReferenceType == "Invoice").ToListAsync());
    }

    [Fact]
    public async Task Handle_DecrementsStock_WhenDirectBillingInvoice()
    {
        var companyId = Guid.NewGuid();
        var productId = Guid.NewGuid();
        var warehouseId = Guid.NewGuid();
        var invoiceId = Guid.NewGuid();
        var tenant = new FakeTenantContext();
        tenant.SetTenant(companyId, "Empresa test");

        var options = new DbContextOptionsBuilder<InventoryDbContext>()
            .UseInMemoryDatabase($"inv-inv-direct-{Guid.NewGuid()}")
            .Options;

        await using var ctx = new InventoryDbContext(options, tenant);
        ctx.Warehouses.Add(new Warehouse { Id = warehouseId, CompanyId = companyId, Name = "Central", IsActive = true });
        ctx.InventoryProducts.Add(new Product
        {
            Id = productId,
            CompanyId = companyId,
            Name = "Producto",
            SKU = "SKU-2",
            CostPrice = 10m,
            TrackStock = true,
        });
        ctx.Stocks.Add(new Stock { CompanyId = companyId, ProductId = productId, WarehouseId = warehouseId, Quantity = 20m });
        await ctx.SaveChangesAsync();

        var handler = new InvoiceApprovedInventoryHandler(ctx, new FakePublisher(), NullLogger<InvoiceApprovedInventoryHandler>.Instance);
        await handler.Handle(new InvoiceApprovedEvent
        {
            InvoiceId = invoiceId,
            CompanyId = companyId,
            SalesOrderId = null,
            Lines =
            [
                new InvoiceLineEventDto { ProductId = productId, Quantity = 3m, UnitPrice = 10m },
            ],
        }, CancellationToken.None);

        Assert.Equal(17m, (await ctx.Stocks.SingleAsync()).Quantity);
        Assert.Single(await ctx.StockMovements.Where(m => m.ReferenceId == invoiceId).ToListAsync());
    }
}
