using Erp.Application.Common.Interfaces;
using Erp.Modules.Billing.Application.Interfaces;
using MediatR;

namespace Erp.Modules.Billing.Application.Features.Billing.Queries;

public record GenerateFacturaEResult(byte[] XmlBytes, string FileName);

public record GenerateFacturaEQuery(Guid InvoiceId) : IRequest<GenerateFacturaEResult>;

public class GenerateFacturaEHandler : IRequestHandler<GenerateFacturaEQuery, GenerateFacturaEResult>
{
    private readonly IFacturaEService _facturaE;
    private readonly ITenantContext _tenant;

    public GenerateFacturaEHandler(IFacturaEService facturaE, ITenantContext tenant)
    {
        _facturaE = facturaE;
        _tenant = tenant;
    }

    public async Task<GenerateFacturaEResult> Handle(GenerateFacturaEQuery request, CancellationToken ct)
    {
        var tenantId = _tenant.TenantId ?? throw new InvalidOperationException("Tenant no resuelto.");
        var (xmlBytes, fileName) = await _facturaE.GenerateAsync(request.InvoiceId, tenantId, ct);
        return new GenerateFacturaEResult(xmlBytes, fileName);
    }
}
