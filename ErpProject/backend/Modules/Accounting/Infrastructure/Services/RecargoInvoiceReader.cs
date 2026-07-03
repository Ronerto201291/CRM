using Erp.Modules.Accounting.Application.Interfaces;
using Erp.Modules.Billing.Application.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace Erp.Modules.Accounting.Infrastructure.Services;

public class RecargoInvoiceReader : IRecargoInvoiceReader
{
    private readonly IBillingDbContext _billing;

    public RecargoInvoiceReader(IBillingDbContext billing) => _billing = billing;

    public async Task<IReadOnlyList<RecargoInvoiceData>> GetLockedInvoicesWithRecargoAsync(
        Guid tenantId,
        DateTime from,
        DateTime to,
        CancellationToken ct)
    {
        var invoices = await _billing.Invoices
            .Include(i => i.InvoiceLines)
            .Where(i => i.CompanyId == tenantId
                     && i.IsLocked
                     && i.IssueDate >= from && i.IssueDate < to
                     && i.InvoiceLines.Any(l => l.SurchargeRate > 0))
            .AsNoTracking()
            .ToListAsync(ct);

        return invoices.Select(MapInvoice).ToList();
    }

    public async Task<RecargoInvoiceData?> GetInvoiceWithRecargoAsync(
        Guid tenantId,
        Guid invoiceId,
        CancellationToken ct)
    {
        var invoice = await _billing.Invoices
            .Include(i => i.InvoiceLines)
            .Where(i => i.CompanyId == tenantId && i.Id == invoiceId
                     && i.InvoiceLines.Any(l => l.SurchargeRate > 0))
            .AsNoTracking()
            .FirstOrDefaultAsync(ct);

        return invoice is null ? null : MapInvoice(invoice);
    }

    public async Task<RecargoInvoiceData?> GetInvoiceAsync(
        Guid tenantId,
        Guid invoiceId,
        CancellationToken ct)
    {
        var invoice = await _billing.Invoices
            .Include(i => i.InvoiceLines)
            .Where(i => i.CompanyId == tenantId && i.Id == invoiceId)
            .AsNoTracking()
            .FirstOrDefaultAsync(ct);

        return invoice is null ? null : MapInvoice(invoice);
    }

    private static RecargoInvoiceData MapInvoice(Erp.Modules.Billing.Domain.Entities.Invoice invoice)
        => new(
            invoice.Id,
            invoice.Number,
            invoice.ClientNif,
            invoice.ClientName,
            invoice.Subtotal,
            invoice.IsLocked,
            invoice.IssueDate,
            invoice.InvoiceLines
                .Where(l => l.SurchargeRate > 0)
                .Select(l => new RecargoInvoiceLineData(l.SurchargeRate, l.SurchargeAmount, l.LineTotal))
                .ToList());
}
