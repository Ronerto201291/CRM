using Erp.Modules.Accounting.Application.Commands;
using Erp.Modules.Accounting.Application.Features.Export;
using Erp.Modules.Accounting.Application.Handlers;
using Erp.Modules.Accounting.Application.Queries;
using Erp.Modules.Accounting.Domain.Entities;
using Erp.Modules.Accounting.Infrastructure.Data;
using Erp.Modules.Crm.Application.Features.Crm.Commands;
using Erp.Modules.Crm.Application.Features.Crm.Handlers;
using Erp.Modules.Crm.Application.Features.Crm.Queries;
using Erp.Modules.Crm.Domain.Entities;
using Erp.Modules.Crm.Infrastructure.Data;
using Erp.Modules.Expenses.Application.Features.Expenses.Commands;
using Erp.Modules.Expenses.Application.Features.Expenses.Handlers;
using Erp.Modules.Expenses.Domain.Entities;
using Erp.Modules.Expenses.Infrastructure.Data;
using Erp.Modules.Inventory.Application.Features.Inventory.Commands;
using Erp.Modules.Inventory.Application.Features.Inventory.Handlers;
using Erp.Modules.Inventory.Application.Features.Inventory.Queries;
using Erp.Modules.Inventory.Domain.Entities;
using Erp.Modules.Inventory.Infrastructure.Data;
using Erp.Modules.Treasury.Application.Features.Treasury.Commands;
using Erp.Modules.Treasury.Application.Features.Treasury.Handlers;
using Erp.Modules.Treasury.Domain.Entities;
using Erp.Modules.Treasury.Infrastructure.Data;
using Erp.Tests.TestSupport;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Erp.Tests.Handlers;

/// <summary>Fase 12: barrido handlers Application restantes (CRM, accounting queries, treasury, inventory, expenses).</summary>
public class Phase12CrmHandlerTests
{
    private static CrmDbContext CreateCrmContext(FakeTenantContext tenant)
    {
        var options = new DbContextOptionsBuilder<CrmDbContext>()
            .UseInMemoryDatabase($"phase12-crm-{Guid.NewGuid()}")
            .Options;
        return new CrmDbContext(options, tenant);
    }

    [Fact]
    public async Task GetClientById_ReturnsClient_WhenExists()
    {
        var companyId = Guid.NewGuid();
        var clientId = Guid.NewGuid();
        var tenant = new FakeTenantContext();
        tenant.SetTenant(companyId, "Empresa test");

        await using var ctx = CreateCrmContext(tenant);
        ctx.Clients.Add(new Client
        {
            Id = clientId, CompanyId = companyId,
            Name = "Cliente fase 12", TaxId = "12345678Z",
            Email = "c12@test.local", CustomFields = "{}",
        });
        await ctx.SaveChangesAsync();

        var result = await new GetClientByIdHandler(ctx).Handle(
            new GetClientByIdQuery { Id = clientId }, CancellationToken.None);

        Assert.NotNull(result);
        Assert.Equal("Cliente fase 12", result!.Name);
    }

    [Fact]
    public async Task GetClientById_ReturnsNull_WhenMissing()
    {
        var tenant = new FakeTenantContext();
        tenant.SetTenant(Guid.NewGuid(), "Empresa test");
        await using var ctx = CreateCrmContext(tenant);

        var result = await new GetClientByIdHandler(ctx).Handle(
            new GetClientByIdQuery { Id = Guid.NewGuid() }, CancellationToken.None);

        Assert.Null(result);
    }

    [Fact]
    public async Task UpdateSupplier_UpdatesFields()
    {
        var companyId = Guid.NewGuid();
        var supplierId = Guid.NewGuid();
        var tenant = new FakeTenantContext();
        tenant.SetTenant(companyId, "Empresa test");

        await using var ctx = CreateCrmContext(tenant);
        ctx.Suppliers.Add(new Supplier
        {
            Id = supplierId, CompanyId = companyId,
            Name = "Proveedor antiguo", TaxId = "B12345674",
            Email = "old@test.local", IsActive = true,
        });
        await ctx.SaveChangesAsync();

        var result = await new UpdateSupplierHandler(ctx).Handle(new UpdateSupplierCommand
        {
            Id = supplierId,
            Name = "Proveedor actualizado",
            TaxId = "B87654321",
            Email = "new@test.local",
            Phone = "600111222",
            Address = "Calle 1",
        }, CancellationToken.None);

        Assert.NotNull(result);
        Assert.Equal("Proveedor actualizado", result!.Name);
        Assert.Equal("new@test.local", (await ctx.Suppliers.SingleAsync()).Email);
    }

    [Fact]
    public async Task UpdateSupplier_Throws_WhenAnonymized()
    {
        var companyId = Guid.NewGuid();
        var supplierId = Guid.NewGuid();
        var tenant = new FakeTenantContext();
        tenant.SetTenant(companyId, "Empresa test");

        await using var ctx = CreateCrmContext(tenant);
        ctx.Suppliers.Add(new Supplier
        {
            Id = supplierId, CompanyId = companyId,
            Name = "Anon", TaxId = "B12345674", IsAnonymized = true,
        });
        await ctx.SaveChangesAsync();

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            new UpdateSupplierHandler(ctx).Handle(new UpdateSupplierCommand
            {
                Id = supplierId, Name = "X", TaxId = "B12345674",
            }, CancellationToken.None));
    }
}

public class Phase12AccountingQueryHandlerTests
{
    private static AccountingDbContext CreateAccountingContext(FakeTenantContext tenant)
    {
        var options = new DbContextOptionsBuilder<AccountingDbContext>()
            .UseInMemoryDatabase($"phase12-acct-{Guid.NewGuid()}")
            .Options;
        return new AccountingDbContext(options, tenant);
    }

    private static async Task SeedPostedEntryAsync(
        AccountingDbContext ctx, Guid companyId, DateTime date, params (string code, string name, decimal debit, decimal credit)[] lines)
    {
        var entryId = Guid.NewGuid();
        var entry = new JournalEntry
        {
            Id = entryId, CompanyId = companyId, Date = date,
            Reference = "P12", Description = "Test", IsPosted = true,
        };
        ctx.JournalEntries.Add(entry);
        foreach (var (code, name, debit, credit) in lines)
        {
            var acctId = Guid.NewGuid();
            ctx.Accounts.Add(new Account { Id = acctId, CompanyId = companyId, Code = code, Name = name, Type = "Asset" });
            ctx.JournalEntryLines.Add(new JournalEntryLine
            {
                JournalEntryId = entryId, JournalEntry = entry,
                AccountId = acctId, AccountCode = code, AccountName = name,
                Debit = debit, Credit = credit,
            });
        }
        await ctx.SaveChangesAsync();
    }

    [Fact]
    public async Task GetTrialBalance_AggregatesByAccount()
    {
        var companyId = Guid.NewGuid();
        var tenant = new FakeTenantContext();
        tenant.SetTenant(companyId, "Empresa test");
        await using var ctx = CreateAccountingContext(tenant);

        await SeedPostedEntryAsync(ctx, companyId, new DateTime(2026, 4, 1, 0, 0, 0, DateTimeKind.Utc),
            ("572", "Bancos", 1000m, 0m),
            ("410", "Proveedores", 0m, 1000m));

        var lines = await new GetTrialBalanceHandler(ctx).Handle(
            new GetTrialBalanceQuery { Year = 2026 }, CancellationToken.None);

        Assert.Equal(2, lines.Count);
        Assert.Contains(lines, l => l.AccountCode == "572" && l.TotalDebit == 1000m);
    }

    [Fact]
    public async Task GetPyG_CalculatesNetResult()
    {
        var companyId = Guid.NewGuid();
        var tenant = new FakeTenantContext();
        tenant.SetTenant(companyId, "Empresa test");
        await using var ctx = CreateAccountingContext(tenant);

        await SeedPostedEntryAsync(ctx, companyId, new DateTime(2026, 5, 1, 0, 0, 0, DateTimeKind.Utc),
            ("700", "Ventas", 0m, 800m),
            ("572", "Bancos", 800m, 0m),
            ("600", "Compras", 300m, 0m),
            ("572", "Bancos", 0m, 300m));

        var result = await new GetPyGHandler(ctx).Handle(new GetPyGQuery { Year = 2026 }, CancellationToken.None);

        Assert.Equal(2026, result.Ejercicio);
        Assert.Equal(500m, result.ResultadoEjercicio);
    }

    [Fact]
    public async Task GetIVASoportado_SumsAccount472()
    {
        var companyId = Guid.NewGuid();
        var tenant = new FakeTenantContext();
        tenant.SetTenant(companyId, "Empresa test");
        await using var ctx = CreateAccountingContext(tenant);

        await SeedPostedEntryAsync(ctx, companyId, new DateTime(2026, 2, 15, 0, 0, 0, DateTimeKind.Utc),
            ("472", "IVA soportado", 42m, 0m),
            ("410", "Proveedores", 0m, 42m));

        var result = await new GetIVASoportadoHandler(ctx).Handle(
            new GetIVASoportadoQuery { Year = 2026 }, CancellationToken.None);

        Assert.Equal(42m, result.Total);
        Assert.Single(result.Details);
    }

    [Fact]
    public async Task GetIVARepercutido_SumsAccount477()
    {
        var companyId = Guid.NewGuid();
        var tenant = new FakeTenantContext();
        tenant.SetTenant(companyId, "Empresa test");
        await using var ctx = CreateAccountingContext(tenant);

        await SeedPostedEntryAsync(ctx, companyId, new DateTime(2026, 3, 10, 0, 0, 0, DateTimeKind.Utc),
            ("430", "Clientes", 121m, 0m),
            ("477", "IVA repercutido", 0m, 21m),
            ("700", "Ventas", 0m, 100m));

        var result = await new GetIVARepercutidoHandler(ctx).Handle(
            new GetIVARepercutidoQuery { Year = 2026 }, CancellationToken.None);

        Assert.Equal(21m, result.Total);
    }

    [Fact]
    public async Task GetLiquidacionIVA_CalculatesQuarterResult()
    {
        var companyId = Guid.NewGuid();
        var tenant = new FakeTenantContext();
        tenant.SetTenant(companyId, "Empresa test");
        await using var ctx = CreateAccountingContext(tenant);

        await SeedPostedEntryAsync(ctx, companyId, new DateTime(2026, 4, 5, 0, 0, 0, DateTimeKind.Utc),
            ("477", "IVA repercutido", 0m, 100m),
            ("430", "Clientes", 100m, 0m),
            ("472", "IVA soportado", 40m, 0m),
            ("410", "Proveedores", 0m, 40m));

        var result = await new GetLiquidacionIVAHandler(ctx).Handle(
            new GetLiquidacionIVAQuery { Year = 2026, Quarter = 2 }, CancellationToken.None);

        Assert.Equal("T2 2026", result.Trimestre);
        Assert.Equal(60m, result.Resultado);
        Assert.Equal(60m, result.AIngresar);
    }

    [Fact]
    public async Task ExportLibroDiario_ExportsCsvWithLines()
    {
        var companyId = Guid.NewGuid();
        var tenant = new FakeTenantContext();
        tenant.SetTenant(companyId, "Empresa test");
        await using var ctx = CreateAccountingContext(tenant);

        await SeedPostedEntryAsync(ctx, companyId, new DateTime(2026, 1, 10, 0, 0, 0, DateTimeKind.Utc),
            ("572", "Bancos", 500m, 0m),
            ("700", "Ventas", 0m, 500m));

        var result = await new ExportLibroDiarioHandler(ctx, tenant).Handle(
            new ExportLibroDiarioQuery(2026), CancellationToken.None);

        Assert.Equal("LibroDiario_2026.csv", result.FileName);
        var text = System.Text.Encoding.UTF8.GetString(result.Content);
        Assert.Contains("572", text);
        Assert.Contains("Bancos", text);
    }

    [Fact]
    public async Task ExportLibroDiario_WithoutTenant_Throws()
    {
        var tenant = new FakeTenantContext();
        await using var ctx = CreateAccountingContext(tenant);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            new ExportLibroDiarioHandler(ctx, tenant).Handle(
                new ExportLibroDiarioQuery(2026), CancellationToken.None));
    }

    [Fact]
    public async Task GetJournalEntries_ReturnsAllEntries()
    {
        var companyId = Guid.NewGuid();
        var tenant = new FakeTenantContext();
        tenant.SetTenant(companyId, "Empresa test");
        await using var ctx = CreateAccountingContext(tenant);

        await SeedPostedEntryAsync(ctx, companyId, DateTime.UtcNow,
            ("572", "Bancos", 100m, 0m),
            ("700", "Ventas", 0m, 100m));

        var entries = await new GetJournalEntriesHandler(ctx).Handle(
            new GetJournalEntriesQuery(), CancellationToken.None);

        Assert.Single(entries);
        Assert.Equal(2, entries[0].Lines.Count);
    }

    [Fact]
    public async Task GetMayorController_ReturnsAccountMovements()
    {
        var companyId = Guid.NewGuid();
        var tenant = new FakeTenantContext();
        tenant.SetTenant(companyId, "Empresa test");
        await using var ctx = CreateAccountingContext(tenant);

        await SeedPostedEntryAsync(ctx, companyId, new DateTime(2026, 6, 1, 0, 0, 0, DateTimeKind.Utc),
            ("572", "Bancos", 250m, 0m),
            ("700", "Ventas", 0m, 250m));

        var mayor = await new GetMayorControllerHandler(ctx).Handle(
            new GetMayorControllerQuery { AccountCode = "572", Year = 2026 }, CancellationToken.None);

        Assert.Equal("572", mayor.AccountCode);
        Assert.Equal(250m, mayor.Saldo);
        Assert.Single(mayor.Movimientos);
    }

    [Fact]
    public async Task GetProvision_ReturnsDto()
    {
        var companyId = Guid.NewGuid();
        var provisionId = Guid.NewGuid();
        var tenant = new FakeTenantContext();
        tenant.SetTenant(companyId, "Empresa test");
        await using var ctx = CreateAccountingContext(tenant);
        ctx.Provisions.Add(new Provision
        {
            Id = provisionId, CompanyId = companyId,
            Code = "490", Description = "Provisión test", Amount = 1500m,
            DueDate = DateTime.UtcNow.AddMonths(3), Status = "Active",
            CreatedAt = DateTime.UtcNow,
        });
        await ctx.SaveChangesAsync();

        var dto = await new GetProvisionHandler(ctx).Handle(
            new GetProvisionQuery(provisionId), CancellationToken.None);

        Assert.NotNull(dto);
        Assert.Equal(1500m, dto!.Amount);
    }

    [Fact]
    public async Task UpdateProvision_UpdatesActiveProvision()
    {
        var companyId = Guid.NewGuid();
        var provisionId = Guid.NewGuid();
        var tenant = new FakeTenantContext();
        tenant.SetTenant(companyId, "Empresa test");
        await using var ctx = CreateAccountingContext(tenant);
        ctx.Provisions.Add(new Provision
        {
            Id = provisionId, CompanyId = companyId,
            Code = "499", Description = "Antes", Amount = 1000m,
            DueDate = DateTime.UtcNow, Status = "Active", CreatedAt = DateTime.UtcNow,
        });
        await ctx.SaveChangesAsync();

        await new UpdateProvisionHandler(ctx).Handle(new UpdateProvisionCommand(
            provisionId, "Después", 2000m, DateTime.UtcNow.AddMonths(1)), CancellationToken.None);

        var updated = await ctx.Provisions.FindAsync(provisionId);
        Assert.Equal("Después", updated!.Description);
        Assert.Equal(2000m, updated.Amount);
    }

    [Fact]
    public async Task DeleteProvision_Removes_WhenNoLinkedJournal()
    {
        var companyId = Guid.NewGuid();
        var provisionId = Guid.NewGuid();
        var tenant = new FakeTenantContext();
        tenant.SetTenant(companyId, "Empresa test");
        await using var ctx = CreateAccountingContext(tenant);
        ctx.Provisions.Add(new Provision
        {
            Id = provisionId, CompanyId = companyId,
            Code = "490", Description = "Borrar", Amount = 500m,
            DueDate = DateTime.UtcNow, Status = "Active", CreatedAt = DateTime.UtcNow,
        });
        await ctx.SaveChangesAsync();

        await new DeleteProvisionHandler(ctx).Handle(
            new DeleteProvisionCommand(provisionId), CancellationToken.None);

        Assert.Empty(await ctx.Provisions.ToListAsync());
    }
}

public class Phase12TreasuryHandlerTests
{
    private static TreasuryDbContext CreateTreasuryContext(FakeTenantContext tenant)
    {
        var options = new DbContextOptionsBuilder<TreasuryDbContext>()
            .UseInMemoryDatabase($"phase12-treasury-{Guid.NewGuid()}")
            .Options;
        return new TreasuryDbContext(options, tenant);
    }

    [Fact]
    public async Task GetCashEffects_FiltersByStatus()
    {
        var companyId = Guid.NewGuid();
        var tenant = new FakeTenantContext();
        tenant.SetTenant(companyId, "Empresa test");
        await using var ctx = CreateTreasuryContext(tenant);
        ctx.CashEffects.AddRange(
            new CashEffect { CompanyId = companyId, EffectNumber = "E1", ClientName = "A", ClientTaxId = "B1", Amount = 100m, IssueDate = DateTime.UtcNow, DueDate = DateTime.UtcNow.AddDays(30), Status = "Pending" },
            new CashEffect { CompanyId = companyId, EffectNumber = "E2", ClientName = "B", ClientTaxId = "B2", Amount = 200m, IssueDate = DateTime.UtcNow, DueDate = DateTime.UtcNow.AddDays(30), Status = "Paid" });
        await ctx.SaveChangesAsync();

        var result = await new GetCashEffectsHandler(ctx).Handle(
            new GetCashEffectsQuery(Status: "Pending"), CancellationToken.None);

        Assert.Equal(1, result.TotalCount);
        Assert.Equal("Pending", result.Items[0].Status);
    }

    [Fact]
    public async Task GetCashEffectById_ReturnsEffect()
    {
        var companyId = Guid.NewGuid();
        var effectId = Guid.NewGuid();
        var tenant = new FakeTenantContext();
        tenant.SetTenant(companyId, "Empresa test");
        await using var ctx = CreateTreasuryContext(tenant);
        ctx.CashEffects.Add(new CashEffect
        {
            Id = effectId, CompanyId = companyId,
            EffectNumber = "EF-12", ClientName = "Cliente", ClientTaxId = "B12345674",
            Amount = 999m, IssueDate = DateTime.UtcNow, DueDate = DateTime.UtcNow.AddDays(15),
            Status = "Pending",
        });
        await ctx.SaveChangesAsync();

        var dto = await new GetCashEffectByIdHandler(ctx).Handle(
            new GetCashEffectByIdQuery(effectId), CancellationToken.None);

        Assert.NotNull(dto);
        Assert.Equal(999m, dto!.Amount);
    }

    [Fact]
    public async Task UpdateCashEffectStatus_UpdatesStatus()
    {
        var companyId = Guid.NewGuid();
        var effectId = Guid.NewGuid();
        var tenant = new FakeTenantContext();
        tenant.SetTenant(companyId, "Empresa test");
        await using var ctx = CreateTreasuryContext(tenant);
        ctx.CashEffects.Add(new CashEffect
        {
            Id = effectId, CompanyId = companyId,
            EffectNumber = "EF-U", ClientName = "C", ClientTaxId = "B1",
            Amount = 50m, IssueDate = DateTime.UtcNow, DueDate = DateTime.UtcNow.AddDays(10),
            Status = "Pending",
        });
        await ctx.SaveChangesAsync();

        var dto = await new UpdateCashEffectStatusHandler(ctx).Handle(
            new UpdateCashEffectStatusCommand(effectId, "Accepted"), CancellationToken.None);

        Assert.Equal("Accepted", dto.Status);
    }

    [Fact]
    public async Task GetPaymentOrders_ReturnsPaginated()
    {
        var companyId = Guid.NewGuid();
        var tenant = new FakeTenantContext();
        tenant.SetTenant(companyId, "Empresa test");
        await using var ctx = CreateTreasuryContext(tenant);
        ctx.PaymentOrders.Add(new PaymentOrder
        {
            CompanyId = companyId, PaymentType = "Supplier",
            BeneficiaryName = "Proveedor SL", BeneficiaryIban = "ES0000000000000000000001",
            Amount = 300m, Status = "Draft", ScheduledDate = DateTime.UtcNow.AddDays(7),
        });
        await ctx.SaveChangesAsync();

        var result = await new GetPaymentOrdersHandler(ctx).Handle(
            new GetPaymentOrdersQuery(), CancellationToken.None);

        Assert.Equal(1, result.TotalCount);
        Assert.Equal(300m, result.Items[0].Amount);
    }
}

public class Phase12InventoryHandlerTests
{
    private static InventoryDbContext CreateInventoryContext(FakeTenantContext tenant)
    {
        var options = new DbContextOptionsBuilder<InventoryDbContext>()
            .UseInMemoryDatabase($"phase12-inv-{Guid.NewGuid()}")
            .Options;
        return new InventoryDbContext(options, tenant);
    }

    [Fact]
    public async Task GetWarehouses_FiltersActive()
    {
        var companyId = Guid.NewGuid();
        var tenant = new FakeTenantContext();
        tenant.SetTenant(companyId, "Empresa test");
        await using var ctx = CreateInventoryContext(tenant);
        ctx.Warehouses.AddRange(
            new Warehouse { CompanyId = companyId, Name = "Activo", IsActive = true, CreatedAt = DateTime.UtcNow },
            new Warehouse { CompanyId = companyId, Name = "Inactivo", IsActive = false, CreatedAt = DateTime.UtcNow });
        await ctx.SaveChangesAsync();

        var list = await new GetWarehousesHandler(ctx).Handle(
            new GetWarehousesQuery { Active = true }, CancellationToken.None);

        Assert.Single(list);
        Assert.Equal("Activo", list[0].Name);
    }

    [Fact]
    public async Task GetWarehouseById_ReturnsDetail()
    {
        var companyId = Guid.NewGuid();
        var warehouseId = Guid.NewGuid();
        var tenant = new FakeTenantContext();
        tenant.SetTenant(companyId, "Empresa test");
        await using var ctx = CreateInventoryContext(tenant);
        ctx.Warehouses.Add(new Warehouse
        {
            Id = warehouseId, CompanyId = companyId,
            Name = "Central", Location = "Madrid", IsActive = true, CreatedAt = DateTime.UtcNow,
        });
        await ctx.SaveChangesAsync();

        var detail = await new GetWarehouseByIdHandler(ctx).Handle(
            new GetWarehouseByIdQuery { Id = warehouseId }, CancellationToken.None);

        Assert.NotNull(detail);
        Assert.Equal("Central", detail!.Name);
    }

    [Fact]
    public async Task UpdateWarehouse_UpdatesFields()
    {
        var companyId = Guid.NewGuid();
        var warehouseId = Guid.NewGuid();
        var tenant = new FakeTenantContext();
        tenant.SetTenant(companyId, "Empresa test");
        await using var ctx = CreateInventoryContext(tenant);
        ctx.Warehouses.Add(new Warehouse
        {
            Id = warehouseId, CompanyId = companyId,
            Name = "Viejo", Location = "A", IsActive = true, CreatedAt = DateTime.UtcNow,
        });
        await ctx.SaveChangesAsync();

        var ok = await new UpdateWarehouseHandler(ctx).Handle(new UpdateWarehouseCommand
        {
            Id = warehouseId, Name = "Nuevo", Location = "Barcelona",
        }, CancellationToken.None);

        Assert.True(ok);
        Assert.Equal("Nuevo", (await ctx.Warehouses.FindAsync(warehouseId))!.Name);
    }

    [Fact]
    public async Task SetWarehouseActive_TogglesFlag()
    {
        var companyId = Guid.NewGuid();
        var warehouseId = Guid.NewGuid();
        var tenant = new FakeTenantContext();
        tenant.SetTenant(companyId, "Empresa test");
        await using var ctx = CreateInventoryContext(tenant);
        ctx.Warehouses.Add(new Warehouse
        {
            Id = warehouseId, CompanyId = companyId,
            Name = "W", IsActive = true, CreatedAt = DateTime.UtcNow,
        });
        await ctx.SaveChangesAsync();

        var ok = await new SetWarehouseActiveHandler(ctx).Handle(
            new SetWarehouseActiveCommand { Id = warehouseId, IsActive = false }, CancellationToken.None);

        Assert.True(ok);
        Assert.False((await ctx.Warehouses.FindAsync(warehouseId))!.IsActive);
    }
}

public class Phase12ExpenseHandlerTests
{
    [Fact]
    public async Task UpdateExpenseLine_UpdatesLine()
    {
        var companyId = Guid.NewGuid();
        var docId = Guid.NewGuid();
        var lineId = Guid.NewGuid();
        var tenant = new FakeTenantContext();
        tenant.SetTenant(companyId, "Empresa test");

        var options = new DbContextOptionsBuilder<ExpensesDbContext>()
            .UseInMemoryDatabase($"phase12-exp-{Guid.NewGuid()}")
            .Options;
        await using var ctx = new ExpensesDbContext(options, tenant);
        ctx.ExpenseDocuments.Add(new ExpenseDocument
        {
            Id = docId, CompanyId = companyId, InvoiceNumber = "G-1",
            SupplierName = "Prov", SupplierTaxId = "B12345674",
            TaxBase = 100m, VATAmount = 21m, Total = 121m, Status = "Draft",
        });
        ctx.ExpenseDocumentLines.Add(new ExpenseDocumentLine
        {
            Id = lineId, ExpenseDocumentId = docId,
            Description = "Antes", Quantity = 1, UnitPrice = 100m, LineTotal = 100m,
        });
        await ctx.SaveChangesAsync();

        var ok = await new UpdateExpenseLineHandler(ctx).Handle(new UpdateExpenseLineCommand
        {
            ExpenseDocumentId = docId, LineId = lineId,
            Description = "Después", Quantity = 2, UnitPrice = 50m, LineTotal = 100m,
        }, CancellationToken.None);

        Assert.True(ok);
        Assert.Equal("Después", (await ctx.ExpenseDocumentLines.FindAsync(lineId))!.Description);
    }

    [Fact]
    public async Task UpdateExpenseLine_Throws_WhenDocumentLocked()
    {
        var companyId = Guid.NewGuid();
        var docId = Guid.NewGuid();
        var lineId = Guid.NewGuid();
        var tenant = new FakeTenantContext();
        tenant.SetTenant(companyId, "Empresa test");

        var options = new DbContextOptionsBuilder<ExpensesDbContext>()
            .UseInMemoryDatabase($"phase12-exp-lock-{Guid.NewGuid()}")
            .Options;
        await using var ctx = new ExpensesDbContext(options, tenant);
        ctx.ExpenseDocuments.Add(new ExpenseDocument
        {
            Id = docId, CompanyId = companyId, InvoiceNumber = "G-2",
            SupplierName = "Prov", SupplierTaxId = "B12345674",
            TaxBase = 50m, VATAmount = 10.5m, Total = 60.5m,
            Status = "Approved", IsLocked = true,
        });
        ctx.ExpenseDocumentLines.Add(new ExpenseDocumentLine
        {
            Id = lineId, ExpenseDocumentId = docId, Description = "L", Quantity = 1, UnitPrice = 50m, LineTotal = 50m,
        });
        await ctx.SaveChangesAsync();

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            new UpdateExpenseLineHandler(ctx).Handle(new UpdateExpenseLineCommand
            {
                ExpenseDocumentId = docId, LineId = lineId, Description = "X",
            }, CancellationToken.None));
    }
}
