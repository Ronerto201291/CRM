using Erp.Application.Common.Interfaces;
using Erp.Modules.Expenses.Application.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace Erp.Modules.Expenses.Infrastructure.Services;

public sealed class SiiRecibidasExpenseSource : ISiiRecibidasExpenseSource
{
    private readonly IExpensesDbContext _expenses;

    public SiiRecibidasExpenseSource(IExpensesDbContext expenses) => _expenses = expenses;

    public async Task<IReadOnlyList<SiiRecibidaExpenseDto>> GetApprovedExpensesAsync(
        Guid companyId, DateTime periodStart, DateTime periodEnd, CancellationToken ct = default)
    {
        return await _expenses.ExpenseDocuments
            .Where(e => e.CompanyId == companyId
                && e.Status == "Approved"
                && e.IssueDate.HasValue
                && e.IssueDate >= periodStart
                && e.IssueDate < periodEnd)
            .OrderBy(e => e.IssueDate)
            .Select(e => new SiiRecibidaExpenseDto(
                e.Id,
                e.InvoiceNumber,
                e.IssueDate!.Value,
                e.CreatedAt,
                e.SupplierName,
                e.SupplierTaxId,
                e.Total,
                e.TaxBase,
                e.VATAmount,
                e.VATRate))
            .AsNoTracking()
            .ToListAsync(ct);
    }
}
