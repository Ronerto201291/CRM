using MediatR;
using Erp.Application.Common;
using Erp.Application.Common.Interfaces;
using Erp.Modules.Accounting.Application.Interfaces;
using Erp.Modules.Accounting.Domain.Entities;

namespace Erp.Modules.Accounting.Application.Features.Vat;

/// <summary>
/// Valida un NIF-IVA UE vía el servicio oficial VIES (misma fuente que TaxController)
/// y registra la consulta en IntraEuOperations para trazabilidad contable.
/// </summary>
public class ValidateViesCommand : IRequest<ValidateViesResult>
{
    public string CountryCode { get; set; } = string.Empty;
    public string VatNumber { get; set; } = string.Empty;
}

public class ValidateViesHandler : IRequestHandler<ValidateViesCommand, ValidateViesResult>
{
    private readonly IAccountingDbContext _context;
    private readonly IViesService _vies;
    private readonly ITenantContext _tenant;

    public ValidateViesHandler(
        IAccountingDbContext context,
        IViesService vies,
        ITenantContext tenant)
    {
        _context = context;
        _vies = vies;
        _tenant = tenant;
    }

    public async Task<ValidateViesResult> Handle(ValidateViesCommand request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.CountryCode) || request.CountryCode.Length != 2)
            throw new ArgumentException("countryCode debe ser un código ISO-2 de 2 letras, p.ej. 'FR'.");
        if (string.IsNullOrWhiteSpace(request.VatNumber))
            throw new ArgumentException("vatNumber no puede estar vacío.");

        var companyId = _tenant.TenantId ?? throw new InvalidOperationException("Tenant no resuelto.");
        var countryCode = request.CountryCode.ToUpperInvariant();
        var vatNumber = request.VatNumber.Trim();

        var result = await _vies.ValidateAsync(countryCode, vatNumber, cancellationToken);

        var operation = new IntraEuOperation
        {
            CompanyId = companyId,
            Type = "Service",
            CountryCode = countryCode,
            PartnerVatId = $"{countryCode}{vatNumber}",
            ViesStatus = result.IsValid ? "Validated" : "Invalid",
        };

        _context.IntraEuOperations.Add(operation);
        await _context.SaveChangesAsync(cancellationToken);

        return new ValidateViesResult
        {
            ValidationId = operation.Id,
            IsValid = result.IsValid,
            CountryCode = result.CountryCode,
            VatNumber = result.VatNumber,
            Name = result.Name,
            Address = result.Address,
            RequestDate = result.RequestDate,
            ErrorMessage = result.ErrorMessage,
            ValidationStatus = result.IsValid ? "Valid" : "Invalid",
            Advice = ViesResponseMapper.BuildAdvice(result),
        };
    }
}

public class ValidateViesResult
{
    public Guid ValidationId { get; set; }
    public bool IsValid { get; set; }
    public string CountryCode { get; set; } = string.Empty;
    public string VatNumber { get; set; } = string.Empty;
    public string? Name { get; set; }
    public string? Address { get; set; }
    public string? RequestDate { get; set; }
    public string? ErrorMessage { get; set; }
    public string ValidationStatus { get; set; } = string.Empty;
    public string Advice { get; set; } = string.Empty;
}
