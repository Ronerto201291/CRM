using Erp.Modules.Inventory.Application.Features.Inventory.Commands;
using Erp.Modules.Inventory.Application.Features.Inventory.Handlers;
using Erp.Modules.Inventory.Application.Features.Inventory.Queries;
using Erp.Modules.Inventory.Domain.Entities;
using Erp.Modules.Inventory.Infrastructure.Data;
using Erp.Tests.TestSupport;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Erp.Tests.Inventory;

public class ProductHandlerTests
{
    [Fact]
    public async Task CreateProduct_PersistsProduct()
    {
        var companyId = Guid.NewGuid();
        var tenant = new FakeTenantContext();
        tenant.SetTenant(companyId, "Empresa test");

        await using var ctx = CreateContext(tenant);
        var handler = new CreateProductHandler(ctx, tenant);

        var result = await handler.Handle(new CreateProductCommand
        {
            SKU = "SKU-001",
            Name = "Producto test",
            Type = "Stock",
            CostPrice = 10m,
            SalePrice = 20m,
            VatPercent = 21m,
            TrackStock = true,
        }, CancellationToken.None);

        Assert.Equal("SKU-001", result.SKU);
        Assert.Single(await ctx.InventoryProducts.ToListAsync());
    }

    [Fact]
    public async Task CreateProduct_ThrowsOnDuplicateSku()
    {
        var companyId = Guid.NewGuid();
        var tenant = new FakeTenantContext();
        tenant.SetTenant(companyId, "Empresa test");

        await using var ctx = CreateContext(tenant);
        ctx.InventoryProducts.Add(new Product
        {
            Id = Guid.NewGuid(), CompanyId = companyId,
            SKU = "DUP", Name = "Existente", Type = "Stock",
            CostPrice = 1m, SalePrice = 2m, VatPercent = 21m,
            TrackStock = true, IsActive = true,
            CreatedAt = DateTime.UtcNow,
        });
        await ctx.SaveChangesAsync();

        var handler = new CreateProductHandler(ctx, tenant);
        await Assert.ThrowsAsync<InvalidOperationException>(() => handler.Handle(
            new CreateProductCommand { SKU = "DUP", Name = "Duplicado", Type = "Stock" },
            CancellationToken.None));
    }

    [Fact]
    public async Task GetProducts_ReturnsPaginatedList()
    {
        var companyId = Guid.NewGuid();
        var tenant = new FakeTenantContext();
        tenant.SetTenant(companyId, "Empresa test");

        await using var ctx = CreateContext(tenant);
        ctx.InventoryProducts.Add(new Product
        {
            Id = Guid.NewGuid(), CompanyId = companyId,
            SKU = "P1", Name = "Producto 1", Type = "Stock",
            CostPrice = 5m, SalePrice = 10m, VatPercent = 21m,
            TrackStock = true, IsActive = true, CreatedAt = DateTime.UtcNow,
        });
        await ctx.SaveChangesAsync();

        var handler = new GetProductsHandler(ctx);
        var result = await handler.Handle(new GetProductsQuery(), CancellationToken.None);

        Assert.Equal(1, result.TotalCount);
        Assert.Equal("Producto 1", result.Items[0].Name);
    }

    [Fact]
    public async Task GetProductById_ReturnsDetail()
    {
        var productId = Guid.NewGuid();
        var companyId = Guid.NewGuid();
        var tenant = new FakeTenantContext();
        tenant.SetTenant(companyId, "Empresa test");

        await using var ctx = CreateContext(tenant);
        ctx.InventoryProducts.Add(new Product
        {
            Id = productId, CompanyId = companyId,
            SKU = "DET", Name = "Detalle", Type = "Stock",
            CostPrice = 5m, SalePrice = 10m, VatPercent = 21m,
            TrackStock = true, IsActive = true, CreatedAt = DateTime.UtcNow,
        });
        await ctx.SaveChangesAsync();

        var handler = new GetProductByIdHandler(ctx);
        var detail = await handler.Handle(new GetProductByIdQuery { Id = productId }, CancellationToken.None);

        Assert.NotNull(detail);
        Assert.Equal("Detalle", detail!.Name);
    }

    [Fact]
    public async Task UpdateProduct_UpdatesFields()
    {
        var productId = Guid.NewGuid();
        var companyId = Guid.NewGuid();
        var tenant = new FakeTenantContext();
        tenant.SetTenant(companyId, "Empresa test");

        await using var ctx = CreateContext(tenant);
        ctx.InventoryProducts.Add(new Product
        {
            Id = productId, CompanyId = companyId,
            SKU = "UPD", Name = "Old", Type = "Stock",
            CostPrice = 5m, SalePrice = 10m, VatPercent = 21m,
            TrackStock = true, IsActive = true, CreatedAt = DateTime.UtcNow,
        });
        await ctx.SaveChangesAsync();

        var handler = new UpdateProductHandler(ctx);
        var ok = await handler.Handle(new UpdateProductCommand
        {
            Id = productId,
            Name = "Updated",
            SalePrice = 15m,
        }, CancellationToken.None);

        Assert.True(ok);
        var updated = await ctx.InventoryProducts.FindAsync(productId);
        Assert.Equal("Updated", updated!.Name);
        Assert.Equal(15m, updated.SalePrice);
    }

    [Fact]
    public async Task SetProductActive_DeactivatesProduct()
    {
        var productId = Guid.NewGuid();
        var companyId = Guid.NewGuid();
        var tenant = new FakeTenantContext();
        tenant.SetTenant(companyId, "Empresa test");

        await using var ctx = CreateContext(tenant);
        ctx.InventoryProducts.Add(new Product
        {
            Id = productId, CompanyId = companyId,
            SKU = "DEL", Name = "Borrar", Type = "Stock",
            CostPrice = 5m, SalePrice = 10m, VatPercent = 21m,
            TrackStock = true, IsActive = true, CreatedAt = DateTime.UtcNow,
        });
        await ctx.SaveChangesAsync();

        var handler = new SetProductActiveHandler(ctx);
        var ok = await handler.Handle(new SetProductActiveCommand { Id = productId, IsActive = false }, CancellationToken.None);

        Assert.True(ok);
        var deleted = await ctx.InventoryProducts.FindAsync(productId);
        Assert.False(deleted!.IsActive);
    }

    private static InventoryDbContext CreateContext(FakeTenantContext tenant)
    {
        var options = new DbContextOptionsBuilder<InventoryDbContext>()
            .UseInMemoryDatabase($"inventory-product-{Guid.NewGuid()}")
            .Options;
        return new InventoryDbContext(options, tenant);
    }
}
