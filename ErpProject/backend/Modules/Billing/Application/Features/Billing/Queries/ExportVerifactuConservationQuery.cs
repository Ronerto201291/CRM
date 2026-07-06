using Erp.Application.Common.Interfaces;
using Erp.Modules.Billing.Application.Interfaces;
using MediatR;

namespace Erp.Modules.Billing.Application.Features.Billing.Queries;

public record ExportVerifactuConservationQuery(int Year, int? Month = null)
    : IRequest<VerifactuConservationPackage>;

public class ExportVerifactuConservationHandler
    : IRequestHandler<ExportVerifactuConservationQuery, VerifactuConservationPackage>
{
    private readonly IVerifactuConservationExporter _exporter;
    private readonly ITenantContext _tenant;

    public ExportVerifactuConservationHandler(
        IVerifactuConservationExporter exporter,
        ITenantContext tenant)
    {
        _exporter = exporter;
        _tenant = tenant;
    }

    public async Task<VerifactuConservationPackage> Handle(
        ExportVerifactuConservationQuery request,
        CancellationToken ct)
    {
        var tenantId = _tenant.TenantId
            ?? throw new InvalidOperationException("Tenant not resolved");

        if (request.Month is < 1 or > 12)
            throw new InvalidOperationException("month debe estar entre 1 y 12");

        return await _exporter.ExportAsync(tenantId, request.Year, request.Month, ct);
    }
}
