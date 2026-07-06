using Erp.Application.Common.Interfaces;
using Erp.Modules.Accounting.Application.Handlers;
using Erp.Modules.Accounting.Domain.Entities;
using Erp.Modules.Accounting.Infrastructure.Data;
using Erp.Tests.TestSupport;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Erp.Tests.Accounting;

public class CostCenterHandlerTests
{
    [Fact]
    public async Task CreateCostCenter_PersistsCenter()
    {
        var companyId = Guid.NewGuid();
        var tenant = new FakeTenantContext();
        tenant.SetTenant(companyId, "Empresa test");

        await using var ctx = CreateContext(tenant);
        var handler = new CreateCostCenterHandler(ctx, tenant);

        var id = await handler.Handle(new CreateCostCenterCommand("CC01", "Ventas", "Revenue"), CancellationToken.None);

        var saved = await ctx.CostCenters.SingleAsync(c => c.Id == id);
        Assert.Equal("CC01", saved.Code);
        Assert.Equal(companyId, saved.CompanyId);
    }

    [Fact]
    public async Task CreateCostCenter_ThrowsOnDuplicateCode()
    {
        var companyId = Guid.NewGuid();
        var tenant = new FakeTenantContext();
        tenant.SetTenant(companyId, "Empresa test");

        await using var ctx = CreateContext(tenant);
        ctx.CostCenters.Add(new CostCenter
        {
            Id = Guid.NewGuid(), CompanyId = companyId, Code = "CC01",
            Name = "Existente", Type = "Revenue", IsActive = true, CreatedAt = DateTime.UtcNow,
        });
        await ctx.SaveChangesAsync();

        var handler = new CreateCostCenterHandler(ctx, tenant);
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            handler.Handle(new CreateCostCenterCommand("CC01", "Duplicado", "Revenue"), CancellationToken.None));
    }

    [Fact]
    public async Task GetCostCenters_ReturnsOrderedList()
    {
        var companyId = Guid.NewGuid();
        var tenant = new FakeTenantContext();
        tenant.SetTenant(companyId, "Empresa test");

        await using var ctx = CreateContext(tenant);
        ctx.CostCenters.AddRange(
            new CostCenter { Id = Guid.NewGuid(), CompanyId = companyId, Code = "B", Name = "B", Type = "Cost", IsActive = true, CreatedAt = DateTime.UtcNow },
            new CostCenter { Id = Guid.NewGuid(), CompanyId = companyId, Code = "A", Name = "A", Type = "Cost", IsActive = true, CreatedAt = DateTime.UtcNow });
        await ctx.SaveChangesAsync();

        var handler = new GetCostCentersHandler(ctx);
        var list = await handler.Handle(new GetCostCentersQuery(), CancellationToken.None);

        Assert.Equal(2, list.Count);
        Assert.Equal("A", list[0].Code);
    }

    [Fact]
    public async Task GetCostCenterById_ReturnsDto()
    {
        var ccId = Guid.NewGuid();
        var companyId = Guid.NewGuid();
        var tenant = new FakeTenantContext();
        tenant.SetTenant(companyId, "Empresa test");

        await using var ctx = CreateContext(tenant);
        ctx.CostCenters.Add(new CostCenter
        {
            Id = ccId, CompanyId = companyId, Code = "CC99",
            Name = "Producción", Type = "Cost", IsActive = true, CreatedAt = DateTime.UtcNow,
        });
        await ctx.SaveChangesAsync();

        var handler = new GetCostCenterHandler(ctx);
        var dto = await handler.Handle(new GetCostCenterQuery(ccId), CancellationToken.None);

        Assert.NotNull(dto);
        Assert.Equal("Producción", dto!.Name);
    }

    [Fact]
    public async Task UpdateCostCenter_UpdatesFields()
    {
        var ccId = Guid.NewGuid();
        await using var ctx = CreateContext(new FakeTenantContext());
        ctx.CostCenters.Add(new CostCenter
        {
            Id = ccId, CompanyId = Guid.NewGuid(), Code = "CC01",
            Name = "Old", Type = "Cost", IsActive = true, CreatedAt = DateTime.UtcNow,
        });
        await ctx.SaveChangesAsync();

        var handler = new UpdateCostCenterHandler(ctx);
        await handler.Handle(new UpdateCostCenterCommand(ccId, "Nuevo nombre", "Revenue", false), CancellationToken.None);

        var updated = await ctx.CostCenters.FindAsync(ccId);
        Assert.Equal("Nuevo nombre", updated!.Name);
        Assert.False(updated.IsActive);
    }

    [Fact]
    public async Task DeleteCostCenter_RemovesWhenNoAllocations()
    {
        var ccId = Guid.NewGuid();
        await using var ctx = CreateContext(new FakeTenantContext());
        ctx.CostCenters.Add(new CostCenter
        {
            Id = ccId, CompanyId = Guid.NewGuid(), Code = "DEL",
            Name = "Borrar", Type = "Cost", IsActive = true, CreatedAt = DateTime.UtcNow,
        });
        await ctx.SaveChangesAsync();

        var handler = new DeleteCostCenterHandler(ctx);
        await handler.Handle(new DeleteCostCenterCommand(ccId), CancellationToken.None);

        Assert.Equal(0, await ctx.CostCenters.CountAsync());
    }

    private static AccountingDbContext CreateContext(FakeTenantContext tenant)
    {
        var options = new DbContextOptionsBuilder<AccountingDbContext>()
            .UseInMemoryDatabase($"cost-center-{Guid.NewGuid()}")
            .Options;
        return new AccountingDbContext(options, tenant);
    }
}
