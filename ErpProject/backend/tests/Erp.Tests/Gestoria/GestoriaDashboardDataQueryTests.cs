using Erp.Application.Common.Interfaces;
using Erp.Infrastructure.Services;
using Erp.Tests.TestSupport;
using Xunit;

namespace Erp.Tests.Gestoria;

/// <summary>
/// Cubre la corrección de la regresión de dirección de dependencias (ADR-0018 #13,
/// contra-auditoría jul 2026): esta clase vive en Erp.Infrastructure (core) y antes
/// inyectaba IBillingDbContext/IExpensesDbContext directamente (de Modules/*/Application),
/// violando la regla de que el core no depende de módulos. Ahora solo compone puertos
/// IAutomationXxxQuery ya existentes — este test verifica que el cálculo sigue siendo
/// correcto tras el refactor, no solo que compile.
/// </summary>
public class GestoriaDashboardDataQueryTests
{
    [Fact]
    public async Task GetCompanyKpi_SumsFromAutomationPorts_NotDirectDbContexts()
    {
        var companyId = Guid.NewGuid();
        var otherCompanyId = Guid.NewGuid();

        var billing = new FakeAutomationBillingQuery();
        billing.IssuedSince.Add(new AutomationInvoiceSnapshot(
            Guid.NewGuid(), companyId, "A-1", "Cliente", 100m, 82.6m, 17.4m, "Locked", DateTime.UtcNow, DateTime.UtcNow));
        billing.IssuedSince.Add(new AutomationInvoiceSnapshot(
            Guid.NewGuid(), companyId, "A-2", "Cliente", 50m, 41.3m, 8.7m, "Locked", DateTime.UtcNow, DateTime.UtcNow));
        billing.IssuedSince.Add(new AutomationInvoiceSnapshot(
            Guid.NewGuid(), otherCompanyId, "A-3", "Otro", 999m, 999m, 0m, "Locked", DateTime.UtcNow, DateTime.UtcNow));
        billing.Overdue.Add(new AutomationInvoiceSnapshot(
            Guid.NewGuid(), companyId, "A-4", "Cliente", 30m, 24.8m, 5.2m, "Sent", DateTime.UtcNow.AddDays(-5), DateTime.UtcNow));

        var expenses = new FakeAutomationExpensesQuery();
        expenses.ApprovedSince.Add(new AutomationExpenseSnapshot(Guid.NewGuid(), companyId, "Proveedor", 40m, DateTime.UtcNow));

        var inventory = new FakeAutomationInventoryQuery();
        inventory.BelowReorder.Add(new AutomationProductStockSnapshot(Guid.NewGuid(), companyId, "Producto", "SKU1", 10m, 20m, 2m));

        var purchasing = new FakeAutomationPurchasingQuery();

        var query = new GestoriaDashboardDataQuery(billing, inventory, expenses, purchasing);

        var kpi = await query.GetCompanyKpiAsync(companyId, CancellationToken.None);

        Assert.Equal(150m, kpi.MonthlyBilling); // 100 + 50, excluye la otra empresa
        Assert.Equal(40m, kpi.MonthlyExpenses);
        Assert.Equal(1, kpi.OverdueInvoices);
        Assert.Equal(1, kpi.LowStockProducts);
    }
}
