using Erp.Application.Common.Interfaces;
using Erp.Modules.Expenses.Application.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace Erp.Modules.Expenses.Infrastructure.Services;

public sealed class AutomationExpensesQuery : IAutomationExpensesQuery
{
    private readonly IExpensesDbContext _expenses;

    public AutomationExpensesQuery(IExpensesDbContext expenses) => _expenses = expenses;

    public async Task<IReadOnlyList<AutomationExpenseSnapshot>> GetPendingPayablesAsync(
        Guid companyId, DateTime horizonEnd, CancellationToken ct = default)
    {
        return await _expenses.ExpenseDocuments
            .IgnoreQueryFilters()
            .Where(e => e.CompanyId == companyId
                && e.Status != "Approved"
                && e.Status != "Rejected"
                && e.IssueDate.HasValue
                && e.IssueDate.Value.Date <= horizonEnd.Date)
            .Select(e => new AutomationExpenseSnapshot(
                e.Id, e.CompanyId, e.SupplierName, e.Total ?? 0, e.IssueDate))
            .AsNoTracking()
            .ToListAsync(ct);
    }

    public async Task<IReadOnlyList<AutomationPendingExpenseApproval>> GetPendingExpenseApprovalsAsync(
        CancellationToken ct = default)
    {
        return await _expenses.ExpenseDocuments
            .IgnoreQueryFilters()
            .Where(e => e.Status == "PendingApproval")
            .Select(e => new AutomationPendingExpenseApproval(
                e.Id, e.CompanyId, e.SupplierName, e.Total ?? 0, e.CreatedAt))
            .AsNoTracking()
            .ToListAsync(ct);
    }
}
