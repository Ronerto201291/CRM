using MediatR;
using Erp.Modules.Accounting.Application.Interfaces;
using Erp.Modules.Accounting.Domain.Entities;

namespace Erp.Modules.Accounting.Application.Features.Vat;

public class CalculateVatCommand : IRequest<CalculateVatResponse>
{
    public Guid CompanyId { get; set; }
    public decimal Amount { get; set; }
    public string VatType { get; set; } = "Standard";
    public bool IsIntraEU { get; set; }
    public bool HasISP { get; set; }
    public bool HasRecargo { get; set; }
}

public class CalculateVatHandler : IRequestHandler<CalculateVatCommand, CalculateVatResponse>
{
    private readonly IAccountingDbContext _context;

    private static readonly Dictionary<string, decimal> VatRates = new()
    {
        { "Standard", 0.21m },
        { "Reduced", 0.10m },
        { "SuperReduced", 0.04m },
        { "Zero", 0m }
    };

    public CalculateVatHandler(IAccountingDbContext context) => _context = context;

    public async Task<CalculateVatResponse> Handle(CalculateVatCommand request, CancellationToken cancellationToken)
    {
        var response = new CalculateVatResponse
        {
            Amount = request.Amount,
            VatType = request.VatType,
            IsIntraEU = request.IsIntraEU
        };

        // Determinar tasa base
        var baseVatRate = request.IsIntraEU ? 0m : GetVatRate(request.VatType);
        response.VatRate = baseVatRate;

        // Calcular IVA
        if (request.HasISP && request.IsIntraEU)
        {
            response.VatAmount = 0;
            response.ISPApplied = true;
            response.ISPAmount = request.Amount * baseVatRate;
        }
        else
        {
            response.VatAmount = request.Amount * baseVatRate;
        }

        // Calcular Recargo
        if (request.HasRecargo && !request.HasISP && !request.IsIntraEU)
        {
            var recargoRate = GetRecargoRate(request.VatType);
            response.RecargoAmount = request.Amount * recargoRate;
        }

        response.Total = request.Amount + response.VatAmount + response.RecargoAmount;

        // Guardar transacción
        var vatTransaction = new VatTransaction
        {
            CompanyId = request.CompanyId,
            TransactionDate = DateTime.UtcNow,
            PaymentDate = DateTime.UtcNow.AddDays(30),
            Direction = "Outbound",
            VatRegime = request.VatType,
            VatableBase = request.Amount,
            VatRate = baseVatRate,
            VatAmount = response.VatAmount,
            DeductibleVat = response.VatAmount,
            Status = "Pending"
        };

        _context.VatTransactions.Add(vatTransaction);
        await _context.SaveChangesAsync(cancellationToken);

        response.TransactionId = vatTransaction.Id;
        return response;
    }

    private static decimal GetVatRate(string vatType)
    {
        return VatRates.TryGetValue(vatType, out var rate) ? rate : 0.21m;
    }

    private static decimal GetRecargoRate(string vatType)
    {
        return vatType switch
        {
            "Standard" => 0.052m,
            "Reduced" => 0.014m,
            "SuperReduced" => 0.005m,
            _ => 0.052m
        };
    }
}

public class CalculateVatResponse
{
    public Guid TransactionId { get; set; }
    public decimal Amount { get; set; }
    public string VatType { get; set; } = string.Empty;
    public decimal VatRate { get; set; }
    public decimal VatAmount { get; set; }
    public decimal RecargoAmount { get; set; }
    public decimal ISPAmount { get; set; }
    public bool ISPApplied { get; set; }
    public bool IsIntraEU { get; set; }
    public decimal Total { get; set; }
}
