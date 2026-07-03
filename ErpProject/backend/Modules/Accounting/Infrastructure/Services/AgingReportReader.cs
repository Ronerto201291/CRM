using Erp.Modules.Accounting.Application.Interfaces;
using Erp.Modules.Billing.Application.Interfaces;
using Erp.Modules.Expenses.Application.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace Erp.Modules.Accounting.Infrastructure.Services;

/// <summary>
/// Cobros (DSO) desde facturas emitidas no pagadas; pagos (DPO) desde gastos aprobados pendientes de tesorería.
/// </summary>
public class AgingReportReader : IAgingReportReader
{
    private readonly IBillingDbContext _billing;
    private readonly IExpensesDbContext _expenses;

    public AgingReportReader(IBillingDbContext billing, IExpensesDbContext expenses)
    {
        _billing = billing;
        _expenses = expenses;
    }

    public async Task<AgingReportData> GetReportAsync(Guid companyId, DateTime? asOf, CancellationToken ct = default)
    {
        var reportDate = (asOf ?? DateTime.UtcNow).Date;

        var unpaidInvoices = await _billing.Invoices
            .Where(i => i.CompanyId == companyId
                && i.Status != "Paid"
                && i.Status != "Cancelled"
                && i.Status != "Draft")
            .Select(i => new
            {
                i.Id,
                i.Number,
                i.ClientName,
                i.IssueDate,
                i.DueDate,
                i.Total,
                i.Status,
            })
            .AsNoTracking()
            .ToListAsync(ct);

        var receivableLines = unpaidInvoices
            .Select(i =>
            {
                var daysPastDue = (reportDate - i.DueDate.Date).Days;
                var daysFromIssue = Math.Max(0, (reportDate - i.IssueDate.Date).Days);
                return new AgingLineRow(
                    i.Id,
                    i.Number,
                    i.ClientName ?? "—",
                    i.DueDate,
                    daysPastDue,
                    daysFromIssue,
                    i.Total,
                    i.Status);
            })
            .OrderByDescending(l => l.DaysOutstanding)
            .ToList();

        var receivables = BuildBucket("Receivables", receivableLines, l => l.DaysOutstanding);

        var openExpenses = await _expenses.ExpenseDocuments
            .Where(e => e.CompanyId == companyId
                && e.Status == "Approved"
                && e.Total != null
                && e.Total > 0)
            .Select(e => new
            {
                e.Id,
                e.InvoiceNumber,
                e.SupplierName,
                e.IssueDate,
                e.Total,
                e.Status,
            })
            .AsNoTracking()
            .ToListAsync(ct);

        var payableLines = openExpenses
            .Select(e =>
            {
                var issue = (e.IssueDate ?? reportDate).Date;
                var daysOpen = Math.Max(0, (reportDate - issue).Days);
                return new AgingLineRow(
                    e.Id,
                    e.InvoiceNumber ?? e.Id.ToString()[..8],
                    e.SupplierName ?? "—",
                    issue,
                    daysOpen,
                    daysOpen,
                    e.Total ?? 0,
                    e.Status);
            })
            .OrderByDescending(l => l.DaysOutstanding)
            .ToList();

        var payables = BuildBucket("Payables", payableLines, l => l.DaysOutstanding);

        var dso = WeightedDays(receivableLines, l => l.DaysFromIssue);
        var dpo = WeightedDays(payableLines, l => l.DaysFromIssue);

        return new AgingReportData(
            reportDate,
            receivables,
            payables,
            dso,
            dpo,
            "Cobros desde facturas Billing no pagadas; pagos desde gastos Expenses aprobados (sin estado de pago en tesorería).");
    }

    private static AgingBucketRow BuildBucket(
        string type,
        IReadOnlyList<AgingLineRow> lines,
        Func<AgingLineRow, int> daysPastDue)
    {
        decimal current = 0, d31 = 0, d61 = 0, d91 = 0;

        foreach (var line in lines)
        {
            var days = daysPastDue(line);
            if (days <= 30)
                current += line.Amount;
            else if (days <= 60)
                d31 += line.Amount;
            else if (days <= 90)
                d61 += line.Amount;
            else
                d91 += line.Amount;
        }

        var total = current + d31 + d61 + d91;

        return new AgingBucketRow(type, total, current, d31, d61, d91, lines);
    }

    private static decimal WeightedDays(
        IReadOnlyList<AgingLineRow> lines,
        Func<AgingLineRow, int> daysSelector)
    {
        var total = lines.Sum(l => l.Amount);
        if (total <= 0) return 0;
        var weighted = lines.Sum(l => l.Amount * daysSelector(l));
        return Math.Round(weighted / total, 1);
    }
}
