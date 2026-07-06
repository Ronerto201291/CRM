using Erp.Application.Common.Interfaces;

namespace Erp.Infrastructure.Services;

public sealed class GestoriaDashboardDataQuery : IGestoriaDashboardDataQuery
{
    private readonly IAutomationBillingQuery _automationBilling;
    private readonly IAutomationInventoryQuery _automationInventory;
    private readonly IAutomationExpensesQuery _automationExpenses;
    private readonly IAutomationPurchasingQuery _automationPurchasing;

    public GestoriaDashboardDataQuery(
        IAutomationBillingQuery automationBilling,
        IAutomationInventoryQuery automationInventory,
        IAutomationExpensesQuery automationExpenses,
        IAutomationPurchasingQuery automationPurchasing)
    {
        _automationBilling = automationBilling;
        _automationInventory = automationInventory;
        _automationExpenses = automationExpenses;
        _automationPurchasing = automationPurchasing;
    }

    public async Task<GestoriaCompanyKpiSnapshot> GetCompanyKpiAsync(Guid companyId, CancellationToken ct = default)
    {
        var monthStart = new DateTime(DateTime.UtcNow.Year, DateTime.UtcNow.Month, 1, 0, 0, 0, DateTimeKind.Utc);

        var monthlyBilling = (await _automationBilling.GetInvoicesIssuedSinceAsync(companyId, monthStart, ct))
            .Sum(i => i.Total);

        var monthlyExpenses = (await _automationExpenses.GetApprovedExpensesSinceAsync(companyId, monthStart, ct))
            .Sum(e => e.Total);

        var today = DateTime.UtcNow.Date;
        var overdue = (await _automationBilling.GetOverdueInvoicesAsync(today, ct))
            .Count(i => i.CompanyId == companyId);

        var lowStock = (await _automationInventory.GetProductsBelowReorderForCompanyAsync(companyId, ct)).Count;

        var pendingExpenses = (await _automationExpenses.GetPendingExpenseApprovalsAsync(ct))
            .Count(e => e.CompanyId == companyId);
        var pendingPo = (await _automationPurchasing.GetPendingPurchaseOrderApprovalsAsync(ct))
            .Count(p => p.CompanyId == companyId);

        return new GestoriaCompanyKpiSnapshot(
            monthlyBilling, monthlyExpenses, overdue, lowStock, pendingExpenses + pendingPo);
    }
}
