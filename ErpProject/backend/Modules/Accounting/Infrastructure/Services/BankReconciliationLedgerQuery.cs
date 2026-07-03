using Erp.Application.Common.Interfaces;
using Erp.Modules.Accounting.Application.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace Erp.Modules.Accounting.Infrastructure.Services;

public sealed class BankReconciliationLedgerQuery : IBankReconciliationLedgerQuery
{
    private readonly IAccountingDbContext _ctx;

    public BankReconciliationLedgerQuery(IAccountingDbContext ctx) => _ctx = ctx;

    public async Task<IReadOnlyList<BankLedgerLineDto>> GetPostedBankLinesAsync(
        Guid companyId,
        string accountCodePrefix,
        int fromYearInclusive,
        CancellationToken ct = default)
    {
        var lines = await _ctx.JournalEntryLines
            .Include(l => l.JournalEntry)
            .Where(l => l.JournalEntry.CompanyId == companyId
                     && l.AccountCode.StartsWith(accountCodePrefix)
                     && l.JournalEntry.IsPosted
                     && l.JournalEntry.Date.Year >= fromYearInclusive)
            .AsNoTracking()
            .ToListAsync(ct);

        return lines
            .Where(l => l.Credit > 0 || l.Debit > 0)
            .Select(l => new BankLedgerLineDto(
                l.Id,
                l.JournalEntry.Date,
                l.JournalEntry.Description,
                l.AccountName,
                l.Debit,
                l.Credit))
            .ToList();
    }
}
