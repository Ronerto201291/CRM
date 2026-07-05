using Erp.Modules.Accounting.Application.Features.Export;

namespace Erp.Modules.Accounting.Application.Interfaces;

public interface IAccountantExpensePdfExporter
{
    Task<IReadOnlyList<AccountantZipFile>> ExportExpensePdfsAsync(
        Guid tenantId, FiscalExportPeriod period, CancellationToken ct);
}
