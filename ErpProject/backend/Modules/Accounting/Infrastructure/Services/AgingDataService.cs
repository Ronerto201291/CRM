using Erp.Modules.Accounting.Application.Interfaces;
using Erp.Modules.Billing.Application.Interfaces;
using Erp.Modules.Expenses.Application.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace Erp.Modules.Accounting.Infrastructure.Services;

public sealed class AgingDataService : IAgingDataService
{
    private readonly IBillingDbContext _billing;
    private readonly IExpensesDbContext _expenses;

    public AgingDataService(IBillingDbContext billing, IExpensesDbContext expenses)
    {
        _billing = billing;
        _expenses = expenses;
    }

    public Task<AgingBucketsDto> GetReceivablesAgingAsync(Guid companyId, CancellationToken ct)
        => BuildReceivablesAsync(companyId, ct);

    public Task<AgingBucketsDto> GetPayablesAgingAsync(Guid companyId, CancellationToken ct)
        => BuildPayablesAsync(companyId, ct);

    public async Task<decimal> CalculateDsoAsync(Guid companyId, CancellationToken ct)
    {
        var aging = await BuildReceivablesAsync(companyId, ct);
        return aging.Dso;
    }

    public async Task<decimal> CalculateDpoAsync(Guid companyId, CancellationToken ct)
    {
        var aging = await BuildPayablesAsync(companyId, ct);
        return aging.Dpo;
    }

    private async Task<AgingBucketsDto> BuildReceivablesAsync(Guid companyId, CancellationToken ct)
    {
        var today = DateTime.UtcNow.Date;
        var invoices = await _billing.Invoices
            .AsNoTracking()
            .Where(i => i.CompanyId == companyId
                        && i.IsLocked
                        && i.Status != "Paid"
                        && i.Total > 0)
            .Select(i => new { i.Total, i.DueDate, i.IssueDate })
            .ToListAsync(ct);

        var buckets = EmptyBuckets("Receivables");
        if (invoices.Count == 0)
            return buckets;

        decimal weightedDays = 0;
        foreach (var inv in invoices)
        {
            var daysPastDue = Math.Max(0, (today - inv.DueDate.Date).Days);
            buckets = AddToBucket(buckets, inv.Total, daysPastDue);
            weightedDays += inv.Total * Math.Max(0, (today - inv.IssueDate.Date).Days);
        }

        var dso = buckets.TotalAmount > 0 ? Math.Round(weightedDays / buckets.TotalAmount, 2) : 0;
        return buckets with { Dso = dso };
    }

    private async Task<AgingBucketsDto> BuildPayablesAsync(Guid companyId, CancellationToken ct)
    {
        var today = DateTime.UtcNow.Date;
        var expenses = await _expenses.ExpenseDocuments
            .AsNoTracking()
            .Where(e => e.CompanyId == companyId
                        && e.Status == "Approved"
                        && (e.Total ?? 0) > 0)
            .Select(e => new { Amount = e.Total ?? 0m, Date = e.IssueDate ?? e.CreatedAt })
            .ToListAsync(ct);

        var buckets = EmptyBuckets("Payables");
        if (expenses.Count == 0)
            return buckets;

        decimal weightedDays = 0;
        foreach (var exp in expenses)
        {
            var reference = exp.Date.Date;
            var daysOpen = Math.Max(0, (today - reference).Days);
            buckets = AddToBucket(buckets, exp.Amount, daysOpen);
            weightedDays += exp.Amount * daysOpen;
        }

        var dpo = buckets.TotalAmount > 0 ? Math.Round(weightedDays / buckets.TotalAmount, 2) : 0;
        return buckets with { Dpo = dpo };
    }

    private static AgingBucketsDto EmptyBuckets(string type) =>
        new(type, 0, 0, 0, 0, 0, 0, 0);

    private static AgingBucketsDto AddToBucket(AgingBucketsDto b, decimal amount, int daysPastDue)
    {
        decimal current = b.Current;
        decimal d31 = b.Days31To60;
        decimal d61 = b.Days61To90;
        decimal d91 = b.Days91Plus;

        if (daysPastDue <= 30) current += amount;
        else if (daysPastDue <= 60) d31 += amount;
        else if (daysPastDue <= 90) d61 += amount;
        else d91 += amount;

        return b with
        {
            TotalAmount = b.TotalAmount + amount,
            Current = current,
            Days31To60 = d31,
            Days61To90 = d61,
            Days91Plus = d91
        };
    }
}
