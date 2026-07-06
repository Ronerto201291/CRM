using Erp.Application.Common.Interfaces;
using Erp.Modules.Billing.Application.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace Erp.Modules.Billing.Infrastructure.Services;

public sealed class SiiEmitidasInvoiceSource : ISiiEmitidasInvoiceSource
{
    private readonly IBillingDbContext _billing;

    public SiiEmitidasInvoiceSource(IBillingDbContext billing) => _billing = billing;

    public async Task<IReadOnlyList<SiiEmitidaInvoiceDto>> GetLockedInvoicesAsync(
        Guid companyId, DateTime periodStart, DateTime periodEnd, CancellationToken ct = default)
    {
        var invoices = await _billing.Invoices
            .Include(i => i.InvoiceLines)
            .Where(i => i.CompanyId == companyId
                && i.IsLocked
                && i.IssueDate >= periodStart
                && i.IssueDate < periodEnd)
            .OrderBy(i => i.IssueDate)
            .AsNoTracking()
            .ToListAsync(ct);

        return invoices.Select(inv => new SiiEmitidaInvoiceDto(
            inv.Number,
            inv.IssueDate,
            inv.ClientNif,
            inv.ClientName,
            inv.InvoiceType,
            inv.Total,
            inv.InvoiceLines.Select(l => new SiiEmitidaInvoiceLineDto(l.LineTotal, l.TaxAmount, l.TaxRate)).ToList()
        )).ToList();
    }
}
