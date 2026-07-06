using Erp.Modules.Accounting.Application.Commands;
using Erp.Modules.Accounting.Application.Features.Vat;
using Erp.Modules.Accounting.Application.Handlers;
using Erp.Modules.Accounting.Application.Queries;
using Erp.Modules.Accounting.Domain.Entities;
using Erp.Modules.Accounting.Infrastructure.Data;
using Erp.Modules.Billing.Application.Handlers;
using Erp.Modules.Billing.Domain.Entities;
using Erp.Modules.Billing.Infrastructure.Data;
using Erp.Modules.Inventory.Application.Features.Inventory.Handlers;
using Erp.Modules.Inventory.Application.Features.Inventory.Queries;
using Erp.Modules.Inventory.Domain.Entities;
using Erp.Modules.Inventory.Infrastructure.Data;
using Erp.Tests.TestSupport;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Erp.Tests.Accounting;

public class Phase10AccountingReportHandlerTests
{
    [Fact]
    public async Task GetBalanceSheet_ReturnsEmptySections_WhenNoMovements()
    {
        var companyId = Guid.NewGuid();
        var tenant = new FakeTenantContext();
        tenant.SetTenant(companyId, "Empresa test");

        var options = new DbContextOptionsBuilder<AccountingDbContext>()
            .UseInMemoryDatabase($"bs-empty-{Guid.NewGuid()}")
            .Options;

        await using var ctx = new AccountingDbContext(options, tenant);
        var handler = new GetBalanceSheetHandler(ctx, tenant);

        var result = await handler.Handle(new GetBalanceSheetQuery { FechaCorte = new DateTime(2026, 12, 31) }, CancellationToken.None);

        Assert.True(result.EstaBalanceado);
        Assert.Equal(0m, result.TotalActivo);
    }

    [Fact]
    public async Task GetBalanceSheet_ClassifiesAssetAccount()
    {
        var companyId = Guid.NewGuid();
        var assetId = Guid.NewGuid();
        var liabilityId = Guid.NewGuid();
        var tenant = new FakeTenantContext();
        tenant.SetTenant(companyId, "Empresa test");

        var options = new DbContextOptionsBuilder<AccountingDbContext>()
            .UseInMemoryDatabase($"bs-asset-{Guid.NewGuid()}")
            .Options;

        await using var ctx = new AccountingDbContext(options, tenant);
        var entryId = Guid.NewGuid();
        ctx.Accounts.AddRange(
            new Account { Id = assetId, CompanyId = companyId, Code = "572", Name = "Bancos", Type = "Asset" },
            new Account { Id = liabilityId, CompanyId = companyId, Code = "410", Name = "Proveedores", Type = "Liability" });
        ctx.JournalEntries.Add(new JournalEntry
        {
            Id = entryId,
            CompanyId = companyId,
            Date = new DateTime(2026, 6, 1, 0, 0, 0, DateTimeKind.Utc),
            Reference = "TEST",
            IsPosted = true,
        });
        ctx.JournalEntryLines.AddRange(
            new JournalEntryLine { JournalEntryId = entryId, AccountId = assetId, Debit = 1000m, Credit = 0m },
            new JournalEntryLine { JournalEntryId = entryId, AccountId = liabilityId, Debit = 0m, Credit = 1000m });
        await ctx.SaveChangesAsync();

        var result = await new GetBalanceSheetHandler(ctx, tenant).Handle(
            new GetBalanceSheetQuery { FechaCorte = new DateTime(2026, 12, 31) }, CancellationToken.None);

        Assert.True(result.Activo.Total > 0);
    }

    [Fact]
    public async Task GetProfitAndLoss_ReturnsSections_WhenRevenueAndExpenseExist()
    {
        var companyId = Guid.NewGuid();
        var revenueId = Guid.NewGuid();
        var expenseId = Guid.NewGuid();
        var assetId = Guid.NewGuid();
        var tenant = new FakeTenantContext();
        tenant.SetTenant(companyId, "Empresa test");

        var options = new DbContextOptionsBuilder<AccountingDbContext>()
            .UseInMemoryDatabase($"pyg-{Guid.NewGuid()}")
            .Options;

        await using var ctx = new AccountingDbContext(options, tenant);
        ctx.Accounts.AddRange(
            new Account { Id = revenueId, CompanyId = companyId, Code = "700", Name = "Ventas", Type = "Income" },
            new Account { Id = expenseId, CompanyId = companyId, Code = "600", Name = "Compras", Type = "Expense" },
            new Account { Id = assetId, CompanyId = companyId, Code = "572", Name = "Bancos", Type = "Asset" });

        var entryId = Guid.NewGuid();
        ctx.JournalEntries.Add(new JournalEntry
        {
            Id = entryId,
            CompanyId = companyId,
            Date = new DateTime(2026, 3, 15, 0, 0, 0, DateTimeKind.Utc),
            Reference = "PYG",
            IsPosted = true,
        });
        ctx.JournalEntryLines.AddRange(
            new JournalEntryLine { JournalEntryId = entryId, AccountId = revenueId, Debit = 0m, Credit = 500m },
            new JournalEntryLine { JournalEntryId = entryId, AccountId = assetId, Debit = 500m, Credit = 0m },
            new JournalEntryLine { JournalEntryId = entryId, AccountId = expenseId, Debit = 200m, Credit = 0m },
            new JournalEntryLine { JournalEntryId = entryId, AccountId = assetId, Debit = 0m, Credit = 200m });
        await ctx.SaveChangesAsync();

        var result = await new GetProfitAndLossHandler(ctx, tenant).Handle(new GetProfitAndLossQuery
        {
            FechaInicio = new DateTime(2026, 1, 1),
            FechaFin = new DateTime(2026, 12, 31),
        }, CancellationToken.None);

        Assert.True(result.TotalIngresos > 0);
        Assert.True(result.TotalGastos > 0);
    }

    [Fact]
    public async Task CreateFixedAsset_PersistsAsset()
    {
        var companyId = Guid.NewGuid();
        var tenant = new FakeTenantContext();
        tenant.SetTenant(companyId, "Empresa test");

        var options = new DbContextOptionsBuilder<AccountingDbContext>()
            .UseInMemoryDatabase($"fa-create-{Guid.NewGuid()}")
            .Options;

        await using var ctx = new AccountingDbContext(options, tenant);
        var id = await new CreateFixedAssetHandler(ctx, tenant).Handle(new CreateFixedAssetCommand(
            AssetCode: "FA-001",
            Name: "Ordenador",
            Description: null,
            AcquisitionDate: new DateTime(2026, 1, 1),
            CommissioningDate: new DateTime(2026, 1, 1),
            AcquisitionCost: 1200m,
            ResidualValue: 0m,
            UsefulLifeYears: 4,
            AmortizationMethod: "Linear",
            AssetAccountCode: "217",
            DepreciationAccountCode: "681",
            AccumDepreciationAccountCode: "281",
            Notes: null), CancellationToken.None);

        var asset = await ctx.FixedAssets.SingleAsync(a => a.Id == id);
        Assert.Equal("Active", asset.Status);
        Assert.Equal(1200m, asset.AcquisitionCost);
    }

    [Fact]
    public async Task GetFixedAssets_ReturnsCompanyAssets()
    {
        var companyId = Guid.NewGuid();
        var tenant = new FakeTenantContext();
        tenant.SetTenant(companyId, "Empresa test");

        var options = new DbContextOptionsBuilder<AccountingDbContext>()
            .UseInMemoryDatabase($"fa-list-{Guid.NewGuid()}")
            .Options;

        await using var ctx = new AccountingDbContext(options, tenant);
        ctx.FixedAssets.Add(new FixedAsset
        {
            CompanyId = companyId,
            AssetCode = "FA-002",
            Name = "Vehículo",
            AcquisitionDate = DateTime.UtcNow,
            AcquisitionCost = 20000m,
            UsefulLifeYears = 5,
            AmortizationMethod = "Linear",
            AssetAccountCode = "218",
            DepreciationAccountCode = "681",
            AccumDepreciationAccountCode = "281",
            Status = "Active",
            CreatedAt = DateTime.UtcNow,
        });
        await ctx.SaveChangesAsync();

        var assets = await new GetFixedAssetsHandler(ctx).Handle(new GetFixedAssetsQuery(), CancellationToken.None);

        Assert.Single(assets);
        Assert.Equal("Vehículo", assets[0].Name);
    }
}

public class Phase10ViesAndBillingHandlerTests
{
    [Fact]
    public async Task ValidateVies_PersistsIntraEuOperation()
    {
        var companyId = Guid.NewGuid();
        var tenant = new FakeTenantContext();
        tenant.SetTenant(companyId, "Empresa test");

        var options = new DbContextOptionsBuilder<AccountingDbContext>()
            .UseInMemoryDatabase($"vies-{Guid.NewGuid()}")
            .Options;

        await using var ctx = new AccountingDbContext(options, tenant);
        var handler = new ValidateViesHandler(ctx, new FakeViesService(), tenant);

        var result = await handler.Handle(new ValidateViesCommand { CountryCode = "FR", VatNumber = "12345678901" }, CancellationToken.None);

        Assert.True(result.IsValid);
        Assert.Equal(1, await ctx.IntraEuOperations.CountAsync());
    }

    [Fact]
    public async Task ValidateVies_InvalidCountry_Throws()
    {
        var tenant = new FakeTenantContext();
        tenant.SetTenant(Guid.NewGuid(), "Empresa test");

        var options = new DbContextOptionsBuilder<AccountingDbContext>()
            .UseInMemoryDatabase($"vies-bad-{Guid.NewGuid()}")
            .Options;

        await using var ctx = new AccountingDbContext(options, tenant);
        var handler = new ValidateViesHandler(ctx, new FakeViesService(), tenant);

        await Assert.ThrowsAsync<ArgumentException>(() =>
            handler.Handle(new ValidateViesCommand { CountryCode = "FRA", VatNumber = "123" }, CancellationToken.None));
    }

    [Fact]
    public async Task CalculateProrrata_PersistsCalculation()
    {
        var companyId = Guid.NewGuid();
        var tenant = new FakeTenantContext();
        tenant.SetTenant(companyId, "Empresa test");

        var options = new DbContextOptionsBuilder<AccountingDbContext>()
            .UseInMemoryDatabase($"prorrata-{Guid.NewGuid()}")
            .Options;

        await using var ctx = new AccountingDbContext(options, tenant);
        var result = await new CalculateProrrataHandler(ctx, tenant).Handle(new CalculateProrrataCommand
        {
            FiscalYear = 2026,
            InlandRevenue = 8000m,
            ExemptRevenue = 2000m,
            Type = "General",
        }, CancellationToken.None);

        Assert.Equal(80m, result.ProrrataPercentage);
        Assert.Single(await ctx.ProrrataCalculations.ToListAsync());
    }

    [Fact]
    public async Task GetInvoicesByStatus_FiltersByStatus()
    {
        var companyId = Guid.NewGuid();
        var tenant = new FakeTenantContext();
        tenant.SetTenant(companyId, "Empresa test");

        var options = new DbContextOptionsBuilder<BillingDbContext>()
            .UseInMemoryDatabase($"inv-status-{Guid.NewGuid()}")
            .Options;

        await using var ctx = new BillingDbContext(options, tenant);
        ctx.Invoices.AddRange(
            new Invoice { CompanyId = companyId, ClientId = Guid.NewGuid(), Number = "A-1", IssueDate = DateTime.UtcNow, DueDate = DateTime.UtcNow, Status = "Draft", Total = 100 },
            new Invoice { CompanyId = companyId, ClientId = Guid.NewGuid(), Number = "A-2", IssueDate = DateTime.UtcNow, DueDate = DateTime.UtcNow, Status = "Paid", Total = 200 });
        await ctx.SaveChangesAsync();

        var drafts = await new GetInvoicesByStatusHandler(ctx).Handle(
            new GetInvoicesByStatusQuery("Draft", companyId), CancellationToken.None);

        Assert.Single(drafts);
        Assert.Equal("Draft", drafts[0].Status);
    }

    [Fact]
    public async Task CreateInvoiceModule_ReturnsNewInvoiceId()
    {
        var clientId = Guid.NewGuid();
        var tenant = new FakeTenantContext();

        var options = new DbContextOptionsBuilder<BillingDbContext>()
            .UseInMemoryDatabase($"inv-module-{Guid.NewGuid()}")
            .Options;

        await using var ctx = new BillingDbContext(options, tenant);
        var issueDate = new DateTime(2026, 7, 1, 0, 0, 0, DateTimeKind.Utc);

        var id = await new CreateInvoiceModuleHandler(ctx).Handle(new CreateInvoiceModuleCommand(
            clientId, "A", 2026, issueDate, issueDate.AddDays(30),
            [new InvoiceLineDto("Servicio", 1, 100, 21)]), CancellationToken.None);

        Assert.NotEqual(Guid.Empty, id);
        var invoice = await ctx.Invoices.IgnoreQueryFilters().Include(i => i.InvoiceLines).SingleAsync(i => i.Id == id);
        Assert.Equal("Draft", invoice.Status);
        Assert.Equal("A-2026-000001", invoice.Number);
        Assert.Single(invoice.InvoiceLines);
    }
}

public class GetInventoryValuationHandlerTests
{
    [Fact]
    public async Task Handle_ReturnsValuationPerProduct()
    {
        var companyId = Guid.NewGuid();
        var productId = Guid.NewGuid();
        var warehouseId = Guid.NewGuid();
        var tenant = new FakeTenantContext();
        tenant.SetTenant(companyId, "Empresa test");

        var options = new DbContextOptionsBuilder<InventoryDbContext>()
            .UseInMemoryDatabase($"inv-val-{Guid.NewGuid()}")
            .Options;

        await using var ctx = new InventoryDbContext(options, tenant);
        ctx.InventoryProducts.Add(new Product
        {
            Id = productId,
            CompanyId = companyId,
            Name = "Producto A",
            SKU = "SKU-1",
            CostPrice = 10m,
            TrackStock = true,
        });
        ctx.Stocks.Add(new Stock
        {
            CompanyId = companyId,
            ProductId = productId,
            WarehouseId = warehouseId,
            Quantity = 5m,
        });
        ctx.StockMovements.Add(new StockMovement
        {
            CompanyId = companyId,
            ProductId = productId,
            WarehouseId = warehouseId,
            MovementType = "Purchase",
            Quantity = 5m,
            UnitCost = 10m,
            CreatedAt = DateTime.UtcNow,
        });
        await ctx.SaveChangesAsync();

        var result = await new GetInventoryValuationHandler(ctx).Handle(new GetInventoryValuationQuery(), CancellationToken.None);

        Assert.Single(result);
        Assert.Equal(50m, result[0].TotalValue);
    }
}
