using Erp.Application.Common.Interfaces;
using Erp.Modules.Accounting.Application.Interfaces;
using MediatR;

namespace Erp.Modules.Accounting.Application.Features.Export;

public record ExportModelo349Query(int Year, int Quarter, bool AsCsv = false) : IRequest<FiscalCsvExportResult>;

public class ExportModelo349Handler : IRequestHandler<ExportModelo349Query, FiscalCsvExportResult>
{
    private readonly IModelo349Exporter _exporter;
    private readonly ITenantContext _tenant;

    public ExportModelo349Handler(IModelo349Exporter exporter, ITenantContext tenant)
    {
        _exporter = exporter;
        _tenant = tenant;
    }

    public async Task<FiscalCsvExportResult> Handle(ExportModelo349Query request, CancellationToken ct)
    {
        if (request.Quarter < 1 || request.Quarter > 4)
            throw new ArgumentException("Quarter must be 1–4");

        var tenantId = _tenant.TenantId ?? throw new InvalidOperationException("Tenant not resolved");
        return await _exporter.ExportAsync(tenantId, request.Year, request.Quarter, request.AsCsv, ct);
    }
}
