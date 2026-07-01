using Erp.Modules.Expenses.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Erp.Modules.Expenses.Application.Interfaces;

public interface IExpensesDbContext
{
    DbSet<ExpenseUpload> ExpenseUploads { get; }
    DbSet<ExpenseDocument> ExpenseDocuments { get; }
    DbSet<ExpenseDocumentLine> ExpenseDocumentLines { get; }
    DbSet<AccountingEntry> AccountingEntries { get; }

    Task<int> SaveChangesAsync(CancellationToken cancellationToken);
}
