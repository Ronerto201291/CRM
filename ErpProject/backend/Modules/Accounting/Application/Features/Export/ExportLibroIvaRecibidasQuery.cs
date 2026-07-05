using Erp.Application.Common.Interfaces;
using Erp.Modules.Accounting.Application.Interfaces;
using MediatR;

namespace Erp.Modules.Accounting.Application.Features.Export;

public record ExportLibroIvaRecibidasQuery(int Year) : IRequest<FiscalCsvExportResult>;

public class ExportLibroIvaRecibidasHandler : IRequestHandler<ExportLibroIvaRecibidasQuery, FiscalCsvExportResult>
{
    private readonly ILibroIvaRecibidasExporter _exporter;
    private readonly ITenantContext _tenant;

    public ExportLibroIvaRecibidasHandler(ILibroIvaRecibidasExporter exporter, ITenantContext tenant)
    {
        _exporter = exporter;
        _tenant = tenant;
    }

    public async Task<FiscalCsvExportResult> Handle(ExportLibroIvaRecibidasQuery request, CancellationToken ct)
    {
        var tenantId = _tenant.TenantId ?? throw new InvalidOperationException("Tenant not resolved");
        return await _exporter.ExportAsync(tenantId, FiscalExportPeriod.FullYear(request.Year), ct);
    }
}
