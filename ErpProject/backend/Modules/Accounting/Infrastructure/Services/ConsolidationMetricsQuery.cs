using Erp.Application.Common.Interfaces;
using Erp.Modules.Accounting.Application.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace Erp.Modules.Accounting.Infrastructure.Services;

public sealed class ConsolidationMetricsQuery : IConsolidationMetricsQuery
{
    private readonly IAccountingDbContext _ctx;

    public ConsolidationMetricsQuery(IAccountingDbContext ctx) => _ctx = ctx;

    public async Task<CompanyConsolidationMetrics> GetCompanyMetricsAsync(
        Guid companyId, int fiscalYear, CancellationToken ct = default)
    {
        var lines = await (
            from l in _ctx.JournalEntryLines.IgnoreQueryFilters()
            join je in _ctx.JournalEntries.IgnoreQueryFilters() on l.JournalEntryId equals je.Id
            where je.CompanyId == companyId && je.Date.Year == fiscalYear && je.IsPosted
            select new { l.AccountCode, l.Debit, l.Credit }
        ).ToListAsync(ct);

        if (lines.Count == 0)
            return new CompanyConsolidationMetrics(0, 0, 0, 0, 0);

        static bool StartsWith(string code, char prefix) =>
            code.Length > 0 && code[0] == prefix;

        var revenue = lines.Where(l => StartsWith(l.AccountCode, '7')).Sum(l => l.Credit - l.Debit);
        var expenses = lines.Where(l => StartsWith(l.AccountCode, '6')).Sum(l => l.Debit - l.Credit);
        var assets = lines.Where(l => StartsWith(l.AccountCode, '1') || StartsWith(l.AccountCode, '2') || StartsWith(l.AccountCode, '3'))
            .Sum(l => l.Debit - l.Credit);
        var liabilities = lines.Where(l => StartsWith(l.AccountCode, '4')).Sum(l => l.Credit - l.Debit);
        var equity = lines.Where(l => StartsWith(l.AccountCode, '5') || l.AccountCode.StartsWith("10"))
            .Sum(l => l.Credit - l.Debit);

        return new CompanyConsolidationMetrics(revenue, expenses, assets, liabilities, equity);
    }
}
