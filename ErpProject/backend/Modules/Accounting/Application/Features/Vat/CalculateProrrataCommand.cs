using MediatR;
using Microsoft.EntityFrameworkCore;
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
        var yearTransactions = _context.VatTransactions.Where(t =>
            t.CompanyId == request.CompanyId &&
            t.TransactionDate.Year == request.Year);

        // El esquema actual de VatRegime ("Standard","Reduced","SuperReduced","Zero")
        // no distingue explícitamente operaciones exentas. Se usa "Zero" como
        // aproximación a operaciones sin derecho a deducción hasta que exista un
        // régimen de IVA "Exento" propio en VatTransaction.
        var deductibleOperations = await yearTransactions
            .Where(t => t.Direction == "Outbound" && t.VatRegime != "Zero")
            .SumAsync(t => t.VatableBase, cancellationToken);

        var nonDeductibleOperations = await yearTransactions
            .Where(t => t.Direction == "Outbound" && t.VatRegime == "Zero")
            .SumAsync(t => t.VatableBase, cancellationToken);

        var totalOperations = deductibleOperations + nonDeductibleOperations;
        var prorataProportion = totalOperations > 0
            ? deductibleOperations / totalOperations
            : 0m;

        var totalVatSupported = await yearTransactions
            .Where(t => t.Direction == "Inbound")
            .SumAsync(t => t.DeductibleVat, cancellationToken);

        var deductibleVat = totalVatSupported * prorataProportion;
        var nonDeductibleVat = totalVatSupported - deductibleVat;

        var calculation = new ProrrataCalculation
        {
            CompanyId = request.CompanyId,
            FiscalYear = request.Year,
            Type = request.ProrrataType,
            InlandRevenue = deductibleOperations,
            ExemptRevenue = nonDeductibleOperations,
            ProrrataPercentage = Math.Round(prorataProportion * 100, 2),
            AdjustmentAmount = totalVatSupported - deductibleVat,
        };

        _context.ProrrataCalculations.Add(calculation);
        await _context.SaveChangesAsync(cancellationToken);

        return new ProrrataResponse
        {
            Id = calculation.Id,
            DeductibleOperations = deductibleOperations,
            NonDeductibleOperations = nonDeductibleOperations,
            ProrataProportion = calculation.ProrrataPercentage,
            DeductibleVat = deductibleVat,
            NonDeductibleVat = nonDeductibleVat,
            Message = $"Prorrata {request.ProrrataType} calculada sobre {request.Year}: {calculation.ProrrataPercentage}%"
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
