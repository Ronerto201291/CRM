using Erp.Modules.Inventory.Application.Features.Inventory.Commands;
using Erp.Modules.Inventory.Application.Features.Inventory.Handlers;
using Erp.Modules.Inventory.Application.Features.Inventory.Queries;
using Erp.Modules.Inventory.Domain.Entities;
using Erp.Modules.Inventory.Infrastructure.Data;
using Erp.Tests.TestSupport;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Erp.Tests.Inventory;

public class AdjustStockHandlerTests
{
    [Fact]
    public async Task Handle_IncreasesStock_AndCreatesMovement()
    {
        var companyId = Guid.NewGuid();
        var productId = Guid.NewGuid();
        var warehouseId = Guid.NewGuid();
        var tenant = new FakeTenantContext();
        tenant.SetTenant(companyId, "Empresa test");

        var options = new DbContextOptionsBuilder<InventoryDbContext>()
            .UseInMemoryDatabase($"inv-adjust-{Guid.NewGuid()}")
            .Options;

        await using var ctx = new InventoryDbContext(options, tenant);
        ctx.InventoryProducts.Add(new Product
        {
            Id = productId,
            CompanyId = companyId,
            SKU = "SKU-1",
            Name = "Producto",
            CostPrice = 10m,
            ReorderPoint = 5m,
        });
        ctx.Warehouses.Add(new Warehouse { Id = warehouseId, CompanyId = companyId, Name = "Central", Location = "Madrid" });
        await ctx.SaveChangesAsync();

        var result = await new AdjustStockHandler(ctx, tenant).Handle(new AdjustStockCommand
        {
            ProductId = productId,
            WarehouseId = warehouseId,
            Quantity = 20m,
            UnitCost = 10m,
        }, CancellationToken.None);

        Assert.Equal(20m, result.NewQuantity);
        Assert.Single(await ctx.StockMovements.ToListAsync());
        Assert.Single(await ctx.Stocks.ToListAsync());
    }

    [Fact]
    public async Task Handle_Throws_WhenInsufficientStock()
    {
        var companyId = Guid.NewGuid();
        var productId = Guid.NewGuid();
        var warehouseId = Guid.NewGuid();
        var tenant = new FakeTenantContext();
        tenant.SetTenant(companyId, "Empresa test");

        var options = new DbContextOptionsBuilder<InventoryDbContext>()
            .UseInMemoryDatabase($"inv-adjust-insuf-{Guid.NewGuid()}")
            .Options;

        await using var ctx = new InventoryDbContext(options, tenant);
        ctx.InventoryProducts.Add(new Product { Id = productId, CompanyId = companyId, SKU = "SKU-2", Name = "P2", CostPrice = 5m });
        ctx.Warehouses.Add(new Warehouse { Id = warehouseId, CompanyId = companyId, Name = "Almacén", Location = "BCN" });
        ctx.Stocks.Add(new Stock { CompanyId = companyId, ProductId = productId, WarehouseId = warehouseId, Quantity = 2m });
        await ctx.SaveChangesAsync();

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            new AdjustStockHandler(ctx, tenant).Handle(new AdjustStockCommand
            {
                ProductId = productId,
                WarehouseId = warehouseId,
                Quantity = -5m,
            }, CancellationToken.None));
    }
}

public class GetStockHandlerTests
{
    [Fact]
    public async Task Handle_ReturnsStockForWarehouse()
    {
        var companyId = Guid.NewGuid();
        var productId = Guid.NewGuid();
        var warehouseId = Guid.NewGuid();
        var tenant = new FakeTenantContext();
        tenant.SetTenant(companyId, "Empresa test");

        var options = new DbContextOptionsBuilder<InventoryDbContext>()
            .UseInMemoryDatabase($"inv-get-stock-{Guid.NewGuid()}")
            .Options;

        await using var ctx = new InventoryDbContext(options, tenant);
        ctx.InventoryProducts.Add(new Product { Id = productId, CompanyId = companyId, SKU = "SKU-3", Name = "Prod", CostPrice = 8m, ReorderPoint = 2m });
        ctx.Warehouses.Add(new Warehouse { Id = warehouseId, CompanyId = companyId, Name = "Main", Location = "Sevilla" });
        ctx.Stocks.Add(new Stock { CompanyId = companyId, ProductId = productId, WarehouseId = warehouseId, Quantity = 15m });
        await ctx.SaveChangesAsync();

        var items = await new GetStockHandler(ctx).Handle(new GetStockQuery { WarehouseId = warehouseId }, CancellationToken.None);
        Assert.Single(items);
        Assert.Equal(15m, items[0].Quantity);
    }
}

public class GetStockValuationHandlerTests
{
    [Fact]
    public async Task Handle_AggregatesStockValue()
    {
        var companyId = Guid.NewGuid();
        var productId = Guid.NewGuid();
        var warehouseId = Guid.NewGuid();
        var tenant = new FakeTenantContext();
        tenant.SetTenant(companyId, "Empresa test");

        var options = new DbContextOptionsBuilder<InventoryDbContext>()
            .UseInMemoryDatabase($"inv-valuation-{Guid.NewGuid()}")
            .Options;

        await using var ctx = new InventoryDbContext(options, tenant);
        ctx.InventoryProducts.Add(new Product { Id = productId, CompanyId = companyId, SKU = "SKU-4", Name = "Val", CostPrice = 10m, SalePrice = 15m });
        ctx.Warehouses.Add(new Warehouse { Id = warehouseId, CompanyId = companyId, Name = "W", Location = "Valencia" });
        ctx.Stocks.Add(new Stock { CompanyId = companyId, ProductId = productId, WarehouseId = warehouseId, Quantity = 4m });
        await ctx.SaveChangesAsync();

        var result = await new GetStockValuationHandler(ctx).Handle(new GetStockValuationQuery(), CancellationToken.None);
        Assert.Equal(40m, result.TotalStockCost);
        Assert.Single(result.Lines);
    }
}
