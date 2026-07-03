using Erp.Modules.Accounting.Application.Features.Export;

namespace Erp.Modules.Accounting.Application.Interfaces;

public interface IModelo303XmlExporter
{
    Task<FiscalCsvExportResult> ExportAsync(Guid tenantId, int year, int quarter, CancellationToken ct);
}

public interface IFiscalSkeletonXmlExporter
{
    Task<FiscalCsvExportResult> ExportModelo200Async(Guid tenantId, int year, CancellationToken ct);
    Task<FiscalCsvExportResult> ExportModelo202Async(Guid tenantId, int year, int period, CancellationToken ct);
}

public interface IModelo390XmlExporter
{
    Task<FiscalCsvExportResult> ExportAsync(Guid tenantId, int year, CancellationToken ct);
}
