using Erp.Modules.Accounting.Application.Features.Export;

namespace Erp.Modules.Accounting.Application.Interfaces;

public interface IModelo349Exporter
{
    Task<FiscalCsvExportResult> ExportAsync(Guid tenantId, int year, int quarter, bool asCsv, CancellationToken ct);
}
