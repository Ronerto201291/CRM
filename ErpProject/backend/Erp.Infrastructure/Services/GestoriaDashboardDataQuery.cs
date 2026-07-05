using Erp.Application.Common.Interfaces;
using Erp.Modules.Billing.Application.Interfaces;
using Erp.Modules.Expenses.Application.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace Erp.Infrastructure.Services;

public sealed class GestoriaDashboardDataQuery : IGestoriaDashboardDataQuery
{
    private readonly IBillingDbContext _billing;
    private readonly IExpensesDbContext _expenses;
    private readonly IAutomationBillingQuery _automationBilling;
    private readonly IAutomationInventoryQuery _automationInventory;
    private readonly IAutomationExpensesQuery _automationExpenses;
    private readonly IAutomationPurchasingQuery _automationPurchasing;

    public GestoriaDashboardDataQuery(
        IBillingDbContext billing,
        IExpensesDbContext expenses,
        IAutomationBillingQuery automationBilling,
        IAutomationInventoryQuery automationInventory,
        IAutomationExpensesQuery automationExpenses,
        IAutomationPurchasingQuery automationPurchasing)
    {
        _billing = billing;
        _expenses = expenses;
        _automationBilling = automationBilling;
        _automationInventory = automationInventory;
        _automationExpenses = automationExpenses;
        _automationPurchasing = automationPurchasing;
    }

    public async Task<GestoriaCompanyKpiSnapshot> GetCompanyKpiAsync(Guid companyId, CancellationToken ct = default)
    {
        var monthStart = new DateTime(DateTime.UtcNow.Year, DateTime.UtcNow.Month, 1, 0, 0, 0, DateTimeKind.Utc);

        var monthlyBilling = await _billing.Invoices.IgnoreQueryFilters()
            .Where(i => i.CompanyId == companyId && i.IssueDate >= monthStart && i.Status != "Cancelled" && i.Status != "Draft")
            .SumAsync(i => i.Total, ct);

        var monthlyExpenses = await _expenses.ExpenseDocuments.IgnoreQueryFilters()
            .Where(e => e.CompanyId == companyId && e.Status == "Approved" && e.IssueDate >= monthStart)
            .SumAsync(e => e.Total ?? 0, ct);

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
