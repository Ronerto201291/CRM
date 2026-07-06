using Erp.Domain.Entities.Core;
using Erp.Infrastructure.Data;
using Erp.Infrastructure.Tenancy;
using Microsoft.EntityFrameworkCore;

namespace Erp.IntegrationTests;

/// <summary>
/// Ajustes de licencia/módulos en BBDD para tests HTTP Postgres (ADR-0018 #42c).
/// RegisterCompany deja plan Free; <see cref="UpgradeToEnterpriseAndEnableAllModulesAsync"/>
/// alinea suscripción y TenantModules con el plan Enterprise sembrado en migraciones.
/// </summary>
internal static class IntegrationTestModuleHelper
{
    internal const string EnterprisePlanName = "Enterprise";

    /// <summary>
    /// Módulos con [RequiredModule] en controllers. Fallback si PlanModules Enterprise no está en BBDD.
    /// </summary>
    internal static readonly string[] BusinessModuleNames =
    [
        "CRM", "Billing", "Accounting", "Treasury", "Purchasing",
        "Sales", "Inventory", "Expenses", "Payroll",
    ];

    /// <summary>
    /// Sube la suscripción activa a Enterprise y habilita todos los módulos incluidos en ese plan.
    /// </summary>
    internal static async Task UpgradeToEnterpriseAndEnableAllModulesAsync(string connectionString, Guid companyId)
    {
        var tenant = new TenantContext();
        var options = new DbContextOptionsBuilder<ErpDbContext>()
            .UseNpgsql(connectionString)
            .Options;

        await using var ctx = new ErpDbContext(options, tenant);

        var subscription = await ctx.Subscriptions
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(s => s.CompanyId == companyId && s.IsActive);
        if (subscription != null && subscription.PlanName != EnterprisePlanName)
        {
            subscription.PlanName = EnterprisePlanName;
            await ctx.SaveChangesAsync();
        }

        var moduleNames = await ctx.Plans
            .AsNoTracking()
            .Where(p => p.Name == EnterprisePlanName && p.IsActive)
            .SelectMany(p => p.PlanModules)
            .Where(pm => pm.IsIncluded)
            .Select(pm => pm.ModuleName)
            .ToListAsync();

        if (moduleNames.Count == 0)
            moduleNames = BusinessModuleNames.ToList();

        await EnableTenantModulesAsync(ctx, companyId, moduleNames);
    }

    /// <summary>
    /// Habilita módulos concretos; sube a Enterprise si la suscripción no lo es ya.
    /// Preferir <see cref="UpgradeToEnterpriseAndEnableAllModulesAsync"/> salvo tests que
    /// necesiten un subconjunto tras un setup Free explícito.
    /// </summary>
    internal static async Task EnableModulesAsync(string connectionString, Guid companyId, params string[] moduleNames)
    {
        var tenant = new TenantContext();
        var options = new DbContextOptionsBuilder<ErpDbContext>()
            .UseNpgsql(connectionString)
            .Options;

        await using var ctx = new ErpDbContext(options, tenant);

        var subscription = await ctx.Subscriptions
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(s => s.CompanyId == companyId && s.IsActive);
        if (subscription != null && subscription.PlanName != EnterprisePlanName)
        {
            subscription.PlanName = EnterprisePlanName;
            await ctx.SaveChangesAsync();
        }

        await EnableTenantModulesAsync(ctx, companyId, moduleNames);
    }

    private static async Task EnableTenantModulesAsync(
        ErpDbContext ctx, Guid companyId, IEnumerable<string> moduleNames)
    {
        var existing = await ctx.TenantModules
            .IgnoreQueryFilters()
            .Where(m => m.CompanyId == companyId)
            .Select(m => m.ModuleName)
            .ToListAsync();
        var set = existing.ToHashSet(StringComparer.Ordinal);

        foreach (var name in moduleNames.Distinct(StringComparer.Ordinal))
        {
            if (set.Contains(name)) continue;
            ctx.TenantModules.Add(new TenantModule
            {
                Id = Guid.NewGuid(),
                CompanyId = companyId,
                ModuleName = name,
                IsEnabled = true,
            });
        }

        await ctx.SaveChangesAsync();
    }
}
