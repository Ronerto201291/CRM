using Erp.Application.Common.Interfaces;
using Erp.Modules.Accounting.Application.Interfaces;
using MediatR;

namespace Erp.Modules.Accounting.Application.Features.Export;

public record ExportModelo303Query(int Year, int Quarter) : IRequest<FiscalCsvExportResult>;

public class ExportModelo303Handler : IRequestHandler<ExportModelo303Query, FiscalCsvExportResult>
{
    private readonly IModelo303Exporter _exporter;
    private readonly ITenantContext _tenant;

    public ExportModelo303Handler(IModelo303Exporter exporter, ITenantContext tenant)
    {
        _exporter = exporter;
        _tenant = tenant;
    }

    public async Task<FiscalCsvExportResult> Handle(ExportModelo303Query request, CancellationToken ct)
    {
        if (request.Quarter < 1 || request.Quarter > 4)
            throw new ArgumentException("Quarter must be 1–4");

        var tenantId = _tenant.TenantId ?? throw new InvalidOperationException("Tenant not resolved");
        return await _exporter.ExportAsync(tenantId, request.Year, request.Quarter, ct);
    }
}
