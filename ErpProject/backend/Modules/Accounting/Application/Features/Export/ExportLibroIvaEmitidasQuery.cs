using Erp.Application.Common.Interfaces;
using Erp.Modules.Accounting.Application.Interfaces;
using MediatR;

namespace Erp.Modules.Accounting.Application.Features.Export;

public record ExportLibroIvaEmitidasQuery(int Year) : IRequest<FiscalCsvExportResult>;

public class ExportLibroIvaEmitidasHandler : IRequestHandler<ExportLibroIvaEmitidasQuery, FiscalCsvExportResult>
{
    private readonly ILibroIvaEmitidasExporter _exporter;
    private readonly ITenantContext _tenant;

    public ExportLibroIvaEmitidasHandler(ILibroIvaEmitidasExporter exporter, ITenantContext tenant)
    {
        _exporter = exporter;
        _tenant = tenant;
    }

    public async Task<FiscalCsvExportResult> Handle(ExportLibroIvaEmitidasQuery request, CancellationToken ct)
    {
        var tenantId = _tenant.TenantId ?? throw new InvalidOperationException("Tenant not resolved");
        return await _exporter.ExportAsync(tenantId, request.Year, ct);
    }
}
