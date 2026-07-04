using Erp.Modules.Inventory.Application.Features.Inventory.Commands;
using Erp.Modules.Inventory.Application.Features.Inventory.Handlers;
using Erp.Modules.Inventory.Application.Features.Inventory.Queries;
using Erp.Modules.Inventory.Domain.Entities;
using Erp.Modules.Inventory.Infrastructure.Data;
using Erp.Tests.TestSupport;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Erp.Tests.Inventory;

public class GetLotsHandlerTests
{
    [Fact]
    public async Task Handle_ReturnsAllLotsForTenant()
    {
        var companyId = Guid.NewGuid();
        var tenant = new FakeTenantContext();
        tenant.SetTenant(companyId, "Empresa test");

        var options = new DbContextOptionsBuilder<InventoryDbContext>()
            .UseInMemoryDatabase($"inventory-lots-{Guid.NewGuid()}")
            .Options;

        await using var ctx = new InventoryDbContext(options, tenant);
        ctx.Lots.Add(new Lot
        {
            Id = Guid.NewGuid(),
            CompanyId = companyId,
            ProductId = Guid.NewGuid(),
            LotNumber = "LOT-001",
            Quantity = 10,
            UnitCost = 5m,
        });
        await ctx.SaveChangesAsync();

        var handler = new GetLotsHandler(ctx);
        var result = await handler.Handle(new GetLotsQuery(), CancellationToken.None);

        Assert.Single(result);
        Assert.Equal("LOT-001", result[0].LotNumber);
    }
}

public class CreateLotHandlerTests
{
    [Fact]
    public async Task Handle_PersistsLotForTenant()
    {
        var companyId = Guid.NewGuid();
        var productId = Guid.NewGuid();
        var tenant = new FakeTenantContext();
        tenant.SetTenant(companyId, "Empresa test");

        var options = new DbContextOptionsBuilder<InventoryDbContext>()
            .UseInMemoryDatabase($"inventory-lot-create-{Guid.NewGuid()}")
            .Options;

        await using var ctx = new InventoryDbContext(options, tenant);
        var handler = new CreateLotHandler(ctx, tenant);

        var result = await handler.Handle(new CreateLotCommand
        {
            ProductId = productId,
            LotNumber = "LOT-NEW",
            Quantity = 25,
            UnitCost = 12.5m,
        }, CancellationToken.None);

        Assert.Equal("LOT-NEW", result.LotNumber);
        var stored = await ctx.Lots.SingleAsync();
        Assert.Equal(companyId, stored.CompanyId);
    }
}
