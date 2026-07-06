using Erp.Application.Common.Interfaces;
using Erp.Modules.Accounting.Application.Interfaces;
using MediatR;

namespace Erp.Modules.Accounting.Application.Features.Export;

public record GetModelo303JsonQuery(int Year, int Quarter) : IRequest<Modelo303JsonResult>;

public record Modelo303JsonResult(
    int Year,
    int Quarter,
    string Period,
    string? Nif,
    string? RazonSocial,
    object Devengado,
    object Deducible,
    object Liquidacion);

public class GetModelo303JsonHandler : IRequestHandler<GetModelo303JsonQuery, Modelo303JsonResult>
{
    private readonly IModelo303Reader _reader;
    private readonly ITenantContext _tenant;

    public GetModelo303JsonHandler(IModelo303Reader reader, ITenantContext tenant)
    {
        _reader = reader;
        _tenant = tenant;
    }

    public async Task<Modelo303JsonResult> Handle(GetModelo303JsonQuery request, CancellationToken ct)
    {
        if (request.Quarter < 1 || request.Quarter > 4)
            throw new ArgumentException("quarter debe ser 1–4");

        var tenantId = _tenant.TenantId ?? throw new InvalidOperationException("Tenant not resolved");
        var data = await _reader.GetQuarterAsync(tenantId, request.Year, request.Quarter, ct);

        return new Modelo303JsonResult(
            data.Year,
            data.Quarter,
            $"T{data.Quarter} {data.Year} ({data.From:dd/MM/yyyy} – {data.To.AddDays(-1):dd/MM/yyyy})",
            data.Nif,
            data.RazonSocial,
            new
            {
                nacional = data.Nacional.Select(x => new
                {
                    tipo = $"Nacional {x.Rate:F0}%",
                    x.CasBase, x.CasCuota,
                    baseImponible = x.Base,
                    cuotaIVA = x.Cuota
                }),
                recargo = data.Recargo.Select(x => new
                {
                    tipo = $"Recargo {x.Rate:F1}%",
                    x.CasBase, x.CasCuota,
                    baseImponible = x.Base,
                    cuotaRecargo = x.Cuota
                }),
                entregasIntracomunitarias = new { casilla = "59", baseImponible = data.Intracom },
                exportaciones = new { casilla = "60", baseImponible = data.Exportaciones },
                totalDevengado = new { casilla = "27", valor = data.TotalDevengado },
            },
            new
            {
                operacionesCorrientes = new { casBase = "28", casCuota = "29", cuota = data.IvaDeducible },
                totalDeducible = new { casilla = "45", valor = data.IvaDeducible },
            },
            new
            {
                casilla = "46",
                valor = Math.Abs(data.Resultado),
                tipo = data.Resultado >= 0 ? "AIngresar" : "ACompensar",
            });
    }
}

public record GetModelo130Query(int Year, int Quarter) : IRequest<Modelo130Result>;

public record Modelo130Result(
    int Year,
    int Quarter,
    string Nota,
    object CasillasOrientativas);

public class GetModelo130Handler : IRequestHandler<GetModelo130Query, Modelo130Result>
{
    public Task<Modelo130Result> Handle(GetModelo130Query request, CancellationToken ct)
    {
        if (request.Quarter is < 1 or > 4)
            throw new ArgumentException("quarter 1–4");

        return Task.FromResult(new Modelo130Result(
            request.Year,
            request.Quarter,
            "El 130 requiere ingresos y gastos estimados del trimestre. Conecte con contabilidad analítica o introduzca datos manualmente en la AEAT.",
            new { ingresos = "01", gastos = "02", baseImponible = "03", tipo = "04", cuota = "05" }));
    }
}
