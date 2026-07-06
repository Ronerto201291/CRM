using Erp.Application.Common.Interfaces;
using Erp.Modules.Accounting.Application.Interfaces;
using MediatR;

namespace Erp.Modules.Accounting.Application.Features.Export;

public record ExportModelo347AeatTxtQuery(int Year) : IRequest<FiscalCsvExportResult>;

public class ExportModelo347AeatTxtHandler : IRequestHandler<ExportModelo347AeatTxtQuery, FiscalCsvExportResult>
{
    private readonly IModelo347Exporter _exporter;
    private readonly ITenantContext _tenant;

    public ExportModelo347AeatTxtHandler(IModelo347Exporter exporter, ITenantContext tenant)
    {
        _exporter = exporter;
        _tenant = tenant;
    }

    public async Task<FiscalCsvExportResult> Handle(ExportModelo347AeatTxtQuery request, CancellationToken ct)
    {
        var tenantId = _tenant.TenantId ?? throw new InvalidOperationException("Tenant not resolved");
        return await _exporter.ExportAeatTxtAsync(tenantId, request.Year, ct);
    }
}
