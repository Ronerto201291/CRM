using Erp.Application.Common.Interfaces;
using Erp.Modules.Accounting.Application.Interfaces;
using MediatR;

namespace Erp.Modules.Accounting.Application.Features.Export;

public record Modelo111InvoiceLine(
    string Numero,
    DateTime FechaExpedicion,
    string? ClienteNif,
    string? ClienteNombre,
    decimal BaseRetencion,
    decimal TipoRetencion,
    decimal ImporteRetencion);

public record Modelo111NominaLine(
    string Nif,
    string Nombre,
    int Mes,
    decimal BaseRetencion,
    decimal TipoRetencion,
    decimal ImporteRetencion);

public record Modelo111Result(
    int Year,
    int Quarter,
    string Period,
    string? Nif,
    string? RazonSocial,
    int NumPerceptores,
    decimal BaseRetencionProf,
    decimal ImporteRetencionProf,
    int NumTrabajadores,
    decimal BaseNominas,
    decimal ImporteNominas,
    decimal TotalAIngresar,
    IReadOnlyList<Modelo111InvoiceLine> FacturasProfesionales,
    IReadOnlyList<Modelo111NominaLine> LineasNominas);

public record GetModelo111Query(int Year, int Quarter) : IRequest<Modelo111Result>;

public class GetModelo111Handler : IRequestHandler<GetModelo111Query, Modelo111Result>
{
    private readonly IModelo111Reader _reader;
    private readonly ITenantContext _tenant;

    public GetModelo111Handler(IModelo111Reader reader, ITenantContext tenant)
    {
        _reader = reader;
        _tenant = tenant;
    }

    public async Task<Modelo111Result> Handle(GetModelo111Query request, CancellationToken ct)
    {
        if (request.Quarter < 1 || request.Quarter > 4)
            throw new ArgumentException("quarter debe ser 1–4");

        var tenantId = _tenant.TenantId ?? throw new InvalidOperationException("Tenant not resolved");
        return await _reader.GetAsync(tenantId, request.Year, request.Quarter, ct);
    }
}
