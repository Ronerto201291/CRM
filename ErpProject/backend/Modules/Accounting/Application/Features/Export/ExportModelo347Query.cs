using Erp.Application.Common.Interfaces;
using Erp.Modules.Accounting.Application.Interfaces;
using MediatR;

namespace Erp.Modules.Accounting.Application.Features.Export;

public record ExportModelo347Query(int Year) : IRequest<FiscalCsvExportResult>;

public class ExportModelo347Handler : IRequestHandler<ExportModelo347Query, FiscalCsvExportResult>
{
    private readonly IModelo347Exporter _exporter;
    private readonly ITenantContext _tenant;

    public ExportModelo347Handler(IModelo347Exporter exporter, ITenantContext tenant)
    {
        _exporter = exporter;
        _tenant = tenant;
    }

    public async Task<FiscalCsvExportResult> Handle(ExportModelo347Query request, CancellationToken ct)
    {
        var tenantId = _tenant.TenantId ?? throw new InvalidOperationException("Tenant not resolved");
        return await _exporter.ExportAsync(tenantId, request.Year, ct);
    }
}
