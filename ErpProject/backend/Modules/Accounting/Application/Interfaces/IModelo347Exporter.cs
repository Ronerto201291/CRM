using Erp.Modules.Accounting.Application.Features.Export;

namespace Erp.Modules.Accounting.Application.Interfaces;

public interface IModelo347Exporter
{
    Task<FiscalCsvExportResult> ExportAsync(Guid tenantId, int year, CancellationToken ct);
    Task<FiscalCsvExportResult> ExportAeatTxtAsync(Guid tenantId, int year, CancellationToken ct);
}
