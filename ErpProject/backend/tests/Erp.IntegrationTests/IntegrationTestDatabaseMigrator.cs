using Erp.Infrastructure.Data;
using Erp.Infrastructure.Tenancy;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;

namespace Erp.IntegrationTests;

/// <summary>Aplica migraciones EF de todos los DbContexts (Postgres / Testcontainers).</summary>
public static class IntegrationTestDatabaseMigrator
{
    private static DbContextOptionsBuilder<T> NpgsqlOptions<T>(string connectionString) where T : DbContext
        => new DbContextOptionsBuilder<T>()
            .UseNpgsql(connectionString)
            .ConfigureWarnings(w => w.Ignore(RelationalEventId.PendingModelChangesWarning));

    public static async Task MigrateAllAsync(IServiceProvider services, CancellationToken ct = default)
    {
        await services.GetRequiredService<ErpDbContext>().Database.MigrateAsync(ct);
        await services.GetRequiredService<Erp.Modules.Billing.Infrastructure.Data.BillingDbContext>().Database.MigrateAsync(ct);
        await services.GetRequiredService<Erp.Modules.Crm.Infrastructure.Data.CrmDbContext>().Database.MigrateAsync(ct);
        await services.GetRequiredService<Erp.Modules.Inventory.Infrastructure.Data.InventoryDbContext>().Database.MigrateAsync(ct);
        await services.GetRequiredService<Erp.Modules.Accounting.Infrastructure.Data.AccountingDbContext>().Database.MigrateAsync(ct);
        await services.GetRequiredService<Erp.Modules.Expenses.Infrastructure.Data.ExpensesDbContext>().Database.MigrateAsync(ct);
        await services.GetRequiredService<Erp.Modules.Treasury.Infrastructure.Data.TreasuryDbContext>().Database.MigrateAsync(ct);
        await services.GetRequiredService<Erp.Modules.Payroll.Infrastructure.Data.PayrollDbContext>().Database.MigrateAsync(ct);
        await services.GetRequiredService<Erp.Modules.Purchasing.Infrastructure.Data.PurchasingDbContext>().Database.MigrateAsync(ct);
        await services.GetRequiredService<Erp.Modules.Sales.Infrastructure.Data.SalesDbContext>().Database.MigrateAsync(ct);
    }

    public static async Task MigrateAllAsync(string connectionString, CancellationToken ct = default)
    {
        var tenant = new TenantContext();

        await using (var ctx = new ErpDbContext(NpgsqlOptions<ErpDbContext>(connectionString).Options, tenant))
            await ctx.Database.MigrateAsync(ct);

        await using (var billing = new Erp.Modules.Billing.Infrastructure.Data.BillingDbContext(
            NpgsqlOptions<Erp.Modules.Billing.Infrastructure.Data.BillingDbContext>(connectionString).Options, tenant))
            await billing.Database.MigrateAsync(ct);

        await using (var crm = new Erp.Modules.Crm.Infrastructure.Data.CrmDbContext(
            NpgsqlOptions<Erp.Modules.Crm.Infrastructure.Data.CrmDbContext>(connectionString).Options, tenant))
            await crm.Database.MigrateAsync(ct);

        await using (var inventory = new Erp.Modules.Inventory.Infrastructure.Data.InventoryDbContext(
            NpgsqlOptions<Erp.Modules.Inventory.Infrastructure.Data.InventoryDbContext>(connectionString).Options, tenant))
            await inventory.Database.MigrateAsync(ct);

        await using (var accounting = new Erp.Modules.Accounting.Infrastructure.Data.AccountingDbContext(
            NpgsqlOptions<Erp.Modules.Accounting.Infrastructure.Data.AccountingDbContext>(connectionString).Options, tenant))
            await accounting.Database.MigrateAsync(ct);

        await using (var expenses = new Erp.Modules.Expenses.Infrastructure.Data.ExpensesDbContext(
            NpgsqlOptions<Erp.Modules.Expenses.Infrastructure.Data.ExpensesDbContext>(connectionString).Options, tenant))
            await expenses.Database.MigrateAsync(ct);

        await using (var treasury = new Erp.Modules.Treasury.Infrastructure.Data.TreasuryDbContext(
            NpgsqlOptions<Erp.Modules.Treasury.Infrastructure.Data.TreasuryDbContext>(connectionString).Options, tenant))
            await treasury.Database.MigrateAsync(ct);

        await using (var payroll = new Erp.Modules.Payroll.Infrastructure.Data.PayrollDbContext(
            NpgsqlOptions<Erp.Modules.Payroll.Infrastructure.Data.PayrollDbContext>(connectionString).Options, tenant))
            await payroll.Database.MigrateAsync(ct);

        await using (var purchasing = new Erp.Modules.Purchasing.Infrastructure.Data.PurchasingDbContext(
            NpgsqlOptions<Erp.Modules.Purchasing.Infrastructure.Data.PurchasingDbContext>(connectionString).Options, tenant))
            await purchasing.Database.MigrateAsync(ct);

        await using (var sales = new Erp.Modules.Sales.Infrastructure.Data.SalesDbContext(
            NpgsqlOptions<Erp.Modules.Sales.Infrastructure.Data.SalesDbContext>(connectionString).Options, tenant))
            await sales.Database.MigrateAsync(ct);
    }
}
