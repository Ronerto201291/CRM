using Erp.Application.Common.Interfaces;
using Erp.Modules.Accounting.Application.Interfaces;
using MediatR;

namespace Erp.Modules.Accounting.Application.Features.Export;

public record GetModelo347JsonQuery(int Year) : IRequest<Modelo347JsonResult>;

public record Modelo347JsonResult(
    int Year,
    string? NifDeclarante,
    string? RazonSocial,
    decimal Umbral,
    int NumClientes,
    int NumProveedores,
    IReadOnlyList<Modelo347JsonRow> Clientes,
    IReadOnlyList<Modelo347JsonRow> Proveedores,
    string Disclaimer);

public record Modelo347JsonRow(
    string Nif,
    string Nombre,
    decimal ImporteTotal,
    decimal BaseImponible,
    decimal CuotaIVA,
    decimal CuotaIRPF,
    int NumOperaciones,
    string TipoNif);

public class GetModelo347JsonHandler : IRequestHandler<GetModelo347JsonQuery, Modelo347JsonResult>
{
    private readonly IModelo347Reader _reader;
    private readonly ITenantContext _tenant;

    public GetModelo347JsonHandler(IModelo347Reader reader, ITenantContext tenant)
    {
        _reader = reader;
        _tenant = tenant;
    }

    public async Task<Modelo347JsonResult> Handle(GetModelo347JsonQuery request, CancellationToken ct)
    {
        var tenantId = _tenant.TenantId ?? throw new InvalidOperationException("Tenant not resolved");
        var data = await _reader.GetYearAsync(tenantId, request.Year, ct);

        static Modelo347JsonRow Map(Modelo347OperatorRow r) => new(
            r.Nif,
            r.Nombre,
            r.ImporteTotal,
            r.BaseImponible,
            r.CuotaIVA,
            r.CuotaIRPF,
            r.NumOperaciones,
            r.EsPersonaFisica ? "F" : "J");

        return new Modelo347JsonResult(
            data.Year,
            data.NifDeclarante,
            data.RazonSocial,
            data.Threshold,
            data.Clientes.Count,
            data.Proveedores.Count,
            data.Clientes.Select(Map).ToList(),
            data.Proveedores.Select(Map).ToList(),
            "Resumen interno modelo 347; validar con programa de ayuda AEAT o asesoría antes de presentar.");
    }
}
