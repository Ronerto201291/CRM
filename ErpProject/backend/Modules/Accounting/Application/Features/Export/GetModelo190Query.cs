using Erp.Application.Common.Interfaces;
using Erp.Modules.Accounting.Application.Interfaces;
using MediatR;

namespace Erp.Modules.Accounting.Application.Features.Export;

public record Modelo190Perceptor(
    string? Nif,
    string? Nombre,
    decimal BaseTotal,
    decimal Retencion);

public record Modelo190Result(
    int Year,
    string? Nif,
    string? RazonSocial,
    string Nota,
    IReadOnlyList<Modelo190Perceptor> PerceptoresProfesionales,
    IReadOnlyList<Modelo190Perceptor> PerceptoresTrabajadores,
    decimal TotalRetencionesProf,
    decimal TotalRetencionesTrab);

public record GetModelo190Query(int Year) : IRequest<Modelo190Result>;

public class GetModelo190Handler : IRequestHandler<GetModelo190Query, Modelo190Result>
{
    private readonly IModelo190Reader _reader;
    private readonly ITenantContext _tenant;

    public GetModelo190Handler(IModelo190Reader reader, ITenantContext tenant)
    {
        _reader = reader;
        _tenant = tenant;
    }

    public async Task<Modelo190Result> Handle(GetModelo190Query request, CancellationToken ct)
    {
        var tenantId = _tenant.TenantId ?? throw new InvalidOperationException("Tenant not resolved");
        return await _reader.GetAsync(tenantId, request.Year, ct);
    }
}
