using Erp.Modules.Accounting.Application.Features.Export;

namespace Erp.Modules.Accounting.Application.Interfaces;

public sealed record AccountantZipFile(string ZipPath, byte[] Content);

public interface IAccountantBillingPdfExporter
{
    Task<IReadOnlyList<AccountantZipFile>> ExportLockedInvoicePdfsAsync(
        Guid tenantId, FiscalExportPeriod period, CancellationToken ct);
}
