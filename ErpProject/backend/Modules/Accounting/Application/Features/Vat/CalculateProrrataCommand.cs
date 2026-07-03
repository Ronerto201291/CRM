using Erp.Application.Common.Interfaces;
using Erp.Modules.Accounting.Application.Interfaces;
using Erp.Modules.Accounting.Domain.Entities;
using MediatR;

namespace Erp.Modules.Accounting.Application.Features.Vat;

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
        var companyId = _tenant.TenantId ?? throw new InvalidOperationException("Tenant no resuelto.");
        var total = request.InlandRevenue + request.ExemptRevenue;
        var proportion = total > 0 ? request.InlandRevenue / total : 0m;
        var percentage = Math.Round(proportion * 100, 2);

        var calculation = new ProrrataCalculation
        {
            Id = Guid.NewGuid(),
            CompanyId = companyId,
            FiscalYear = request.FiscalYear,
            Type = request.Type,
            InlandRevenue = request.InlandRevenue,
            ExemptRevenue = request.ExemptRevenue,
            ProrrataPercentage = percentage,
            AdjustmentAmount = 0
        };

        _context.ProrrataCalculations.Add(calculation);
        await _context.SaveChangesAsync(cancellationToken);

        return new ProrrataResponse
        {
            Id = calculation.Id,
            InlandRevenue = request.InlandRevenue,
            ExemptRevenue = request.ExemptRevenue,
            ProrrataPercentage = percentage,
            Message = $"Prorrata {request.Type} calculada: {percentage}%"
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
