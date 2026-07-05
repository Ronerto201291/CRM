using Erp.Application.Common.Interfaces;

namespace Erp.Tests.TestSupport;

/// <summary>Fakes en memoria de los puertos de automatización, para tests de handlers que los
/// componen (p. ej. GetTreasuryLiquidityForecastHandler) sin tener que instanciar el DbContext
/// real de cada módulo de origen.</summary>
public sealed class FakeAutomationBillingQuery : IAutomationBillingQuery
{
    public List<AutomationInvoiceSnapshot> Overdue { get; } = [];
    public List<AutomationInvoiceSnapshot> PendingReceivables { get; } = [];
    public List<AutomationInvoiceSnapshot> IssuedSince { get; } = [];

    public Task<IReadOnlyList<AutomationInvoiceSnapshot>> GetOverdueInvoicesAsync(DateTime today, CancellationToken ct = default)
        => Task.FromResult<IReadOnlyList<AutomationInvoiceSnapshot>>(Overdue);

    public Task<IReadOnlyList<AutomationInvoiceSnapshot>> GetInvoicesForRuleAsync(Guid companyId, string triggerEvent, DateTime today, CancellationToken ct = default)
        => Task.FromResult<IReadOnlyList<AutomationInvoiceSnapshot>>([]);

    public Task<IReadOnlyList<AutomationInvoiceSnapshot>> GetPendingReceivablesAsync(Guid companyId, DateTime horizonEnd, CancellationToken ct = default)
        => Task.FromResult<IReadOnlyList<AutomationInvoiceSnapshot>>(PendingReceivables.Where(i => i.CompanyId == companyId).ToList());

    public Task<IReadOnlyList<AutomationInvoiceSnapshot>> GetInvoicesIssuedSinceAsync(Guid companyId, DateTime since, CancellationToken ct = default)
        => Task.FromResult<IReadOnlyList<AutomationInvoiceSnapshot>>(IssuedSince.Where(i => i.CompanyId == companyId).ToList());
}

public sealed class FakeAutomationExpensesQuery : IAutomationExpensesQuery
{
    public List<AutomationExpenseSnapshot> PendingPayables { get; } = [];
    public List<AutomationExpenseSnapshot> ApprovedSince { get; } = [];
    public List<AutomationPendingExpenseApproval> PendingApprovals { get; } = [];

    public Task<IReadOnlyList<AutomationExpenseSnapshot>> GetPendingPayablesAsync(Guid companyId, DateTime horizonEnd, CancellationToken ct = default)
        => Task.FromResult<IReadOnlyList<AutomationExpenseSnapshot>>(PendingPayables.Where(e => e.CompanyId == companyId).ToList());

    public Task<IReadOnlyList<AutomationPendingExpenseApproval>> GetPendingExpenseApprovalsAsync(CancellationToken ct = default)
        => Task.FromResult<IReadOnlyList<AutomationPendingExpenseApproval>>(PendingApprovals);

    public Task<IReadOnlyList<AutomationExpenseSnapshot>> GetApprovedExpensesSinceAsync(Guid companyId, DateTime since, CancellationToken ct = default)
        => Task.FromResult<IReadOnlyList<AutomationExpenseSnapshot>>(ApprovedSince.Where(e => e.CompanyId == companyId).ToList());
}

public sealed class FakeAutomationInventoryQuery : IAutomationInventoryQuery
{
    /// <summary>Productos con punto de reorden configurado (cualquier empresa), como en la
    /// implementación real: `GetProductsBelowReorderForCompanyAsync` se deriva de esta misma
    /// lista filtrando por empresa y stock.</summary>
    public List<AutomationProductStockSnapshot> BelowReorder { get; } = [];

    public Task<IReadOnlyList<AutomationProductStockSnapshot>> GetProductsWithReorderPointAsync(CancellationToken ct = default)
        => Task.FromResult<IReadOnlyList<AutomationProductStockSnapshot>>(BelowReorder);

    public Task<IReadOnlyList<AutomationProductStockSnapshot>> GetProductsBelowReorderForCompanyAsync(Guid companyId, CancellationToken ct = default)
        => Task.FromResult<IReadOnlyList<AutomationProductStockSnapshot>>(
            BelowReorder.Where(p => p.CompanyId == companyId && p.CurrentStock <= p.ReorderPoint).ToList());
}

public sealed class FakeAutomationPurchasingQuery : IAutomationPurchasingQuery
{
    public List<AutomationPendingPurchaseOrder> PendingApprovals { get; } = [];

    public Task<IReadOnlyList<AutomationPendingPurchaseOrder>> GetPendingPurchaseOrderApprovalsAsync(CancellationToken ct = default)
        => Task.FromResult<IReadOnlyList<AutomationPendingPurchaseOrder>>(PendingApprovals);
}

public sealed class FakeAutomationRecurringQuery : IAutomationRecurringQuery
{
    public decimal MonthlyInflow { get; set; }

    public Task<decimal> GetMonthlyRecurringInflowAsync(Guid companyId, CancellationToken ct = default)
        => Task.FromResult(MonthlyInflow);
}

public sealed class FakeAutomationPayrollQuery : IAutomationPayrollQuery
{
    public decimal MonthlyPayrollCost { get; set; }

    public Task<decimal> GetLatestMonthlyPayrollCostAsync(Guid companyId, CancellationToken ct = default)
        => Task.FromResult(MonthlyPayrollCost);
}
