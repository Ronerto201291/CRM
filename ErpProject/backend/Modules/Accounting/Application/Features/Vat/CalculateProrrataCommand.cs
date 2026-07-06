using MediatR;
using Erp.Application.Common.Interfaces;
using Erp.Modules.Accounting.Application.Interfaces;
using Erp.Modules.Accounting.Domain.Entities;

namespace Erp.Modules.Accounting.Application.Features.Vat;

/// <summary>
/// Calculadora manual de prorrata: el usuario introduce los ingresos sujetos
/// y exentos (frontend/src/app/accounting/prorrata/page.tsx), no se derivan
/// autom├íticamente de las transacciones de IVA ÔÇö coincide con el contrato que
/// ya consume esa p├ígina (fiscalYear/inlandRevenue/exemptRevenue/type).
/// </summary>
public class CalculateProrrataCommand : IRequest<ProrrataResponse>
{
    public int FiscalYear { get; set; }
    public decimal InlandRevenue { get; set; }
    public decimal ExemptRevenue { get; set; }
    public string Type { get; set; } = "General";
}

public class CalculateProrrataHandler : IRequestHandler<CalculateProrrataCommand, ProrrataResponse>
{
    private readonly IAccountingDbContext _context;
    private readonly ITenantContext _tenant;

    public CalculateProrrataHandler(IAccountingDbContext context, ITenantContext tenant)
    {
        _context = context;
        _tenant = tenant;
    }

    public async Task<ProrrataResponse> Handle(CalculateProrrataCommand request, CancellationToken cancellationToken)
    {
        var totalRevenue = request.InlandRevenue + request.ExemptRevenue;
        var prorrataPercentage = totalRevenue > 0
            ? Math.Round(request.InlandRevenue / totalRevenue * 100, 2)
            : 0m;

        var companyId = _tenant.TenantId ?? throw new InvalidOperationException("Tenant no resuelto.");

        var calculation = new ProrrataCalculation
        {
            CompanyId = companyId,
            FiscalYear = request.FiscalYear,
            Type = request.Type,
            InlandRevenue = request.InlandRevenue,
            ExemptRevenue = request.ExemptRevenue,
            ProrrataPercentage = prorrataPercentage,
        };

        _context.ProrrataCalculations.Add(calculation);
        await _context.SaveChangesAsync(cancellationToken);

        return new ProrrataResponse
        {
            Id = calculation.Id,
            InlandRevenue = request.InlandRevenue,
            ExemptRevenue = request.ExemptRevenue,
            ProrrataPercentage = prorrataPercentage,
            Message = $"Prorrata {request.Type} calculada sobre {request.FiscalYear}: {prorrataPercentage}%"
        };
    }
}

public class ProrrataResponse
{
    public Guid Id { get; set; }
    public decimal InlandRevenue { get; set; }
    public decimal ExemptRevenue { get; set; }
    public decimal ProrrataPercentage { get; set; }
    public string Message { get; set; } = string.Empty;
}
