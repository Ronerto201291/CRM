using Erp.Application.Common.Interfaces;
using Erp.Modules.Accounting.Application.Interfaces;
using MediatR;

namespace Erp.Modules.Accounting.Application.Features.Export;

public record ExportModelo390Query(int Year) : IRequest<FiscalCsvExportResult>;

public class ExportModelo390Handler : IRequestHandler<ExportModelo390Query, FiscalCsvExportResult>
{
    private readonly IModelo390Exporter _exporter;
    private readonly ITenantContext _tenant;

    public ExportModelo390Handler(IModelo390Exporter exporter, ITenantContext tenant)
    {
        _exporter = exporter;
        _tenant = tenant;
    }

    public async Task<FiscalCsvExportResult> Handle(ExportModelo390Query request, CancellationToken ct)
    {
        var tenantId = _tenant.TenantId ?? throw new InvalidOperationException("Tenant not resolved");
        return await _exporter.ExportAsync(tenantId, request.Year, ct);
    }
}
