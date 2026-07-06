using Erp.Application.Features.Company.Commands;
using Erp.Application.Features.Company.Handlers;
using Erp.Modules.Crm.Application.Features.Crm.Commands;
using Erp.Modules.Crm.Application.Features.Crm.Handlers;
using Erp.Modules.Expenses.Application.Features.Expenses.Commands;
using Erp.Modules.Expenses.Application.Features.Expenses.Handlers;
using Erp.Modules.Expenses.Domain.Entities;
using Erp.Modules.Expenses.Infrastructure.Data;
using Erp.Modules.Inventory.Application.Features.Inventory.Handlers;
using Erp.Modules.Inventory.Application.Features.Inventory.Queries;
using Erp.Modules.Inventory.Domain.Entities;
using Erp.Modules.Inventory.Infrastructure.Data;
using Erp.Domain.Entities.Core;
using Erp.Infrastructure.Data;
using Erp.Modules.Crm.Infrastructure.Data;
using Erp.Tests.TestSupport;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Erp.Tests.Handlers;

public class RegenerateTokenHandlerTests
{
    [Fact]
    public async Task Handle_GeneratesNewPublicUploadToken()
    {
        var companyId = Guid.NewGuid();
        var tenant = new FakeTenantContext();
        tenant.SetTenant(companyId, "Empresa test");

        var options = new DbContextOptionsBuilder<ErpDbContext>()
            .UseInMemoryDatabase($"company-regen-token-{Guid.NewGuid()}")
            .Options;

        await using var ctx = new ErpDbContext(options, tenant);
        ctx.Companies.Add(new Company
        {
            Id = companyId,
            Name = "Co",
            TaxId = "B12345674",
            PublicUploadToken = "old-token-123",
            QrUploadEnabled = true,
        });
        await ctx.SaveChangesAsync();

        var newToken = await new RegenerateTokenHandler(ctx, tenant).Handle(new RegenerateTokenCommand(), CancellationToken.None);
        Assert.NotEqual("old-token-123", newToken);
        var company = await ctx.Companies.SingleAsync();
        Assert.Equal(newToken, company.PublicUploadToken);
    }
}

public class CreateLeadHandlerTests
{
    [Fact]
    public async Task Handle_PersistsLeadWithDefaultStatus()
    {
        var companyId = Guid.NewGuid();
        var tenant = new FakeTenantContext();
        tenant.SetTenant(companyId, "Empresa test");

        var options = new DbContextOptionsBuilder<CrmDbContext>()
            .UseInMemoryDatabase($"crm-create-lead-{Guid.NewGuid()}")
            .Options;

        await using var ctx = new CrmDbContext(options, tenant);
        var dto = await new CreateLeadHandler(ctx, tenant, new FakePublisher()).Handle(new CreateLeadCommand
        {
            Name = "Nuevo lead",
            Email = "lead@test.local",
            Source = "Web",
        }, CancellationToken.None);

        Assert.Equal("New", dto.Status);
        Assert.Single(await ctx.Leads.ToListAsync());
    }
}

public class AddExpenseLineHandlerTests
{
    [Fact]
    public async Task Handle_AddsLineToDraftExpense()
    {
        var companyId = Guid.NewGuid();
        var docId = Guid.NewGuid();
        var tenant = new FakeTenantContext();
        tenant.SetTenant(companyId, "Empresa test");

        var options = new DbContextOptionsBuilder<ExpensesDbContext>()
            .UseInMemoryDatabase($"exp-add-line-{Guid.NewGuid()}")
            .Options;

        await using var ctx = new ExpensesDbContext(options, tenant);
        ctx.ExpenseDocuments.Add(new ExpenseDocument
        {
            Id = docId,
            CompanyId = companyId,
            SupplierName = "Proveedor",
            Status = "Draft",
        });
        await ctx.SaveChangesAsync();

        var lineId = await new AddExpenseLineHandler(ctx).Handle(new AddExpenseLineCommand
        {
            ExpenseDocumentId = docId,
            Description = "Material",
            Quantity = 2,
            UnitPrice = 15m,
            VATRate = 21m,
            LineTotal = 30m,
        }, CancellationToken.None);

        Assert.NotEqual(Guid.Empty, lineId);
        Assert.Single(await ctx.ExpenseDocumentLines.ToListAsync());
    }
}

public class GetStockMovementsHandlerTests
{
    [Fact]
    public async Task Handle_ReturnsPaginatedMovements()
    {
        var companyId = Guid.NewGuid();
        var productId = Guid.NewGuid();
        var warehouseId = Guid.NewGuid();
        var tenant = new FakeTenantContext();
        tenant.SetTenant(companyId, "Empresa test");

        var options = new DbContextOptionsBuilder<InventoryDbContext>()
            .UseInMemoryDatabase($"inv-movements-{Guid.NewGuid()}")
            .Options;

        await using var ctx = new InventoryDbContext(options, tenant);
        ctx.InventoryProducts.Add(new Product { Id = productId, CompanyId = companyId, SKU = "M1", Name = "Prod", CostPrice = 5m });
        ctx.Warehouses.Add(new Warehouse { Id = warehouseId, CompanyId = companyId, Name = "WH", Location = "Loc" });
        ctx.StockMovements.Add(new StockMovement
        {
            CompanyId = companyId,
            ProductId = productId,
            WarehouseId = warehouseId,
            MovementType = "Entry",
            Quantity = 10m,
            UnitCost = 5m,
            CreatedAt = DateTime.UtcNow,
        });
        await ctx.SaveChangesAsync();

        var result = await new GetStockMovementsHandler(ctx).Handle(new GetStockMovementsQuery { Page = 1, PageSize = 10 }, CancellationToken.None);
        Assert.Equal(1, result.Total);
        Assert.Single(result.Items);
    }
}

public class UpdateCompanyHappyPathHandlerTests
{
    [Fact]
    public async Task Handle_UpdatesCompanyName()
    {
        var companyId = Guid.NewGuid();
        var tenant = new FakeTenantContext();
        tenant.SetTenant(companyId, "Empresa test");

        var options = new DbContextOptionsBuilder<ErpDbContext>()
            .UseInMemoryDatabase($"company-update-ok-{Guid.NewGuid()}")
            .Options;

        await using var ctx = new ErpDbContext(options, tenant);
        ctx.Companies.Add(new Company { Id = companyId, Name = "Antigua", TaxId = "B12345674", Country = "ES" });
        await ctx.SaveChangesAsync();

        var ok = await new UpdateCompanyHandler(ctx, tenant).Handle(new UpdateCompanyCommand { Name = "Nueva razón social" }, CancellationToken.None);
        Assert.True(ok);
        Assert.Equal("Nueva razón social", (await ctx.Companies.SingleAsync()).Name);
    }
}
