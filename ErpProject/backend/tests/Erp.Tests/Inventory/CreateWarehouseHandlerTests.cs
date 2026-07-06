using Erp.Modules.Inventory.Application.Features.Inventory.Commands;
using Erp.Modules.Inventory.Application.Features.Inventory.Handlers;
using Erp.Modules.Inventory.Infrastructure.Data;
using Erp.Tests.TestSupport;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Erp.Tests.Inventory;

public class CreateWarehouseHandlerTests
{
    [Fact]
    public async Task Handle_CreatesWarehouseForTenant()
    {
        var tenant = new FakeTenantContext();
        var companyId = Guid.NewGuid();
        tenant.SetTenant(companyId, "Empresa test");

        var options = new DbContextOptionsBuilder<InventoryDbContext>()
            .UseInMemoryDatabase($"warehouse-{Guid.NewGuid()}")
            .Options;

        await using var ctx = new InventoryDbContext(options, tenant);
        var handler = new CreateWarehouseHandler(ctx, tenant);

        var result = await handler.Handle(new CreateWarehouseCommand
        {
            Name = "Almacén central",
            Location = "Madrid",
        }, CancellationToken.None);

        Assert.Equal("Almacén central", result.Name);
        Assert.Equal("Madrid", result.Location);

        var stored = await ctx.Warehouses.FirstOrDefaultAsync(w => w.Id == result.Id);
        Assert.NotNull(stored);
        Assert.Equal(companyId, stored!.CompanyId);
    }

    [Fact]
    public async Task Handle_WithoutTenant_Throws()
    {
        var tenant = new FakeTenantContext();
        var options = new DbContextOptionsBuilder<InventoryDbContext>()
            .UseInMemoryDatabase($"warehouse-no-tenant-{Guid.NewGuid()}")
            .Options;

        await using var ctx = new InventoryDbContext(options, tenant);
        var handler = new CreateWarehouseHandler(ctx, tenant);

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => handler.Handle(
            new CreateWarehouseCommand { Name = "X" },
            CancellationToken.None));
    }
}
