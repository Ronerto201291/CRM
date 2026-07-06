using Erp.Application.Common.Interfaces;
using Erp.Modules.Accounting.Application.Interfaces;
using Erp.Modules.Accounting.Domain.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Erp.Modules.Accounting.Application.Features.Vat;

public record DeclareModelo330Command(int Year, int Quarter) : IRequest<DeclareModelo330Result>;

public class DeclareModelo330Result
{
    public Guid Id { get; set; }
    public string Modelo { get; set; } = "303";
    public string LegacyModelo { get; set; } = "330";
    public int Year { get; set; }
    public int Quarter { get; set; }
    public decimal TotalDevengado { get; set; }
    public decimal IvaDeducible { get; set; }
    public decimal Resultado { get; set; }
    public string ResultadoTipo { get; set; } = "AIngresar";
    public string Status { get; set; } = "Declared";
    public string Message { get; set; } = string.Empty;
}

public class DeclareModelo330Handler : IRequestHandler<DeclareModelo330Command, DeclareModelo330Result>
{
    private readonly IModelo303Reader _reader;
    private readonly IAccountingDbContext _accounting;
    private readonly ITenantContext _tenant;

    public DeclareModelo330Handler(
        IModelo303Reader reader,
        IAccountingDbContext accounting,
        ITenantContext tenant)
    {
        _reader = reader;
        _accounting = accounting;
        _tenant = tenant;
    }

    public async Task<DeclareModelo330Result> Handle(DeclareModelo330Command request, CancellationToken ct)
    {
        var tenantId = _tenant.TenantId ?? throw new InvalidOperationException("Tenant not resolved");

        if (request.Quarter is < 1 or > 4)
            throw new InvalidOperationException("El trimestre debe estar entre 1 y 4.");

        var existing = await _accounting.VatLiquidations
            .FirstOrDefaultAsync(v => v.CompanyId == tenantId
                                   && v.Year == request.Year
                                   && v.Quarter == request.Quarter, ct);

        if (existing is not null)
        {
            throw new InvalidOperationException(
                $"Ya existe una declaración IVA para T{request.Quarter} {request.Year} " +
                $"(id: {existing.Id}).");
        }

        var data = await _reader.GetQuarterAsync(tenantId, request.Year, request.Quarter, ct);
        var resultadoTipo = data.Resultado >= 0 ? "AIngresar" : "ACompensar";

        var liquidation = new VatLiquidation
        {
            CompanyId = tenantId,
            Year = request.Year,
            Quarter = request.Quarter,
            ModeloCode = "303",
            TotalDevengado = data.TotalDevengado,
            IvaDeducible = data.IvaDeducible,
            Resultado = Math.Abs(data.Resultado),
            ResultadoTipo = resultadoTipo,
            Status = "Declared",
        };

        _accounting.VatLiquidations.Add(liquidation);
        await _accounting.SaveChangesAsync(ct);

        return new DeclareModelo330Result
        {
            Id = liquidation.Id,
            Year = request.Year,
            Quarter = request.Quarter,
            TotalDevengado = data.TotalDevengado,
            IvaDeducible = data.IvaDeducible,
            Resultado = Math.Abs(data.Resultado),
            ResultadoTipo = resultadoTipo,
            Message =
                "Declaración IVA registrada. El endpoint legacy «modelo330» corresponde al " +
                "modelo 303 vigente (el 330 quedó obsoleto en 2014).",
        };
    }
}
