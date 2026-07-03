using Erp.Modules.Accounting.Application.Features.Export;

namespace Erp.Modules.Accounting.Application.Interfaces;

public interface ILibroIvaRecibidasExporter
{
    Task<FiscalCsvExportResult> ExportAsync(Guid tenantId, int year, CancellationToken ct);
}
