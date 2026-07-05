using Erp.Modules.Accounting.Application.Features.Export;

namespace Erp.Modules.Accounting.Application.Interfaces;

public interface ILibroIvaEmitidasExporter
{
    Task<FiscalCsvExportResult> ExportAsync(Guid tenantId, FiscalExportPeriod period, CancellationToken ct);
}
