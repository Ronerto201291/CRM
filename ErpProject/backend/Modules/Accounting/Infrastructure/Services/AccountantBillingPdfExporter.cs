using Erp.Modules.Accounting.Application.Features.Export;
using Erp.Modules.Accounting.Application.Interfaces;
using Erp.Modules.Billing.Application.Features.Billing.Queries;
using Erp.Modules.Billing.Application.Interfaces;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Erp.Modules.Accounting.Infrastructure.Services;

public sealed class AccountantBillingPdfExporter(
    IBillingDbContext billing,
    IMediator mediator) : IAccountantBillingPdfExporter
{
    public async Task<IReadOnlyList<AccountantZipFile>> ExportLockedInvoicePdfsAsync(
        Guid tenantId, FiscalExportPeriod period, CancellationToken ct)
    {
        var from = period.FromUtc;
        var to = period.ToUtc;

        var invoiceIds = await billing.Invoices.AsNoTracking()
            .Where(i => i.CompanyId == tenantId && i.IsLocked && i.IssueDate >= from && i.IssueDate < to)
            .OrderBy(i => i.IssueDate).ThenBy(i => i.Number)
            .Select(i => i.Id)
            .ToListAsync(ct);

        var files = new List<AccountantZipFile>(invoiceIds.Count);
        foreach (var invoiceId in invoiceIds)
        {
            try
            {
                var pdf = await mediator.Send(new GetInvoicePdfQuery(invoiceId), ct);
                var safeName = pdf.FileName.Replace("\\", "-").Replace("/", "-");
                files.Add(new AccountantZipFile($"facturas/{safeName}", pdf.PdfBytes));
            }
            catch
            {
                // Factura no bloqueada o PDF no disponible — omitir sin abortar el paquete completo.
            }
        }

        return files;
    }
}
