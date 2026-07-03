using MediatR;
using Erp.Modules.Accounting.Application.Interfaces;
using Erp.Modules.Accounting.Domain.Entities;

namespace Erp.Modules.Accounting.Application.Features.Vat;

public class CalculateProrrataCommand : IRequest<ProrrataResponse>
{
    public Guid CompanyId { get; set; }
    public int Year { get; set; }
    public string ProrrataType { get; set; } = "General";
}

public class CalculateProrrataHandler : IRequestHandler<CalculateProrrataCommand, ProrrataResponse>
{
    private readonly IAccountingDbContext _context;

    public CalculateProrrataHandler(IAccountingDbContext context) => _context = context;

    public async Task<ProrrataResponse> Handle(CalculateProrrataCommand request, CancellationToken cancellationToken)
    {
        // Simulación: en producción, calcular desde BD
        var deductibleOperations = 100000m;
        var nonDeductibleOperations = 20000m;
        var totalOperations = deductibleOperations + nonDeductibleOperations;
        var prorataProportion = deductibleOperations / totalOperations;
        
        var totalVatSupported = 21000m;
        var deductibleVat = totalVatSupported * prorataProportion;
        var nonDeductibleVat = totalVatSupported - deductibleVat;

        var calculation = new ProrrataCalculation
        {
            Id = Guid.NewGuid(),
            CompanyId = request.CompanyId,
            FiscalYear = request.Year,
            Type = request.ProrrataType,
            InlandRevenue = deductibleOperations,
            ExemptRevenue = nonDeductibleOperations,
            ProrrataPercentage = Math.Round(prorataProportion * 100, 2),
            AdjustmentAmount = nonDeductibleVat
        };

        _context.ProrrataCalculations.Add(calculation);
        await _context.SaveChangesAsync(cancellationToken);

        return new ProrrataResponse
        {
            Id = calculation.Id,
            DeductibleOperations = deductibleOperations,
            NonDeductibleOperations = nonDeductibleOperations,
            ProrataProportion = Math.Round(prorataProportion * 100, 2),
            DeductibleVat = deductibleVat,
            NonDeductibleVat = nonDeductibleVat,
            Message = $"Prorrata {request.ProrrataType} calculada: {Math.Round(prorataProportion * 100, 2)}%"
        };
    }
}

public class ProrrataResponse
{
    public Guid Id { get; set; }
    public decimal DeductibleOperations { get; set; }
    public decimal NonDeductibleOperations { get; set; }
    public decimal ProrataProportion { get; set; }
    public decimal DeductibleVat { get; set; }
    public decimal NonDeductibleVat { get; set; }
    public string Message { get; set; } = string.Empty;
}
