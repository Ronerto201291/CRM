using MediatR;
using Erp.Application.Common.Interfaces;
using Erp.Modules.Accounting.Application.Interfaces;
using Erp.Modules.Accounting.Domain.Entities;

namespace Erp.Modules.Accounting.Application.Features.Vat;

public class ValidateViesCommand : IRequest<ViesValidationResponse>
{
    public string VatNumber { get; set; } = string.Empty;
    public Guid CompanyId { get; set; }
}

public class ValidateViesHandler : IRequestHandler<ValidateViesCommand, ViesValidationResponse>
{
    private readonly IAccountingDbContext _context;
    private readonly IViesService _vies;

    public ValidateViesHandler(IAccountingDbContext context, IViesService vies)
    {
        _context = context;
        _vies = vies;
    }

    public async Task<ViesValidationResponse> Handle(ValidateViesCommand request, CancellationToken cancellationToken)
    {
        var response = new ViesValidationResponse
        {
            VatNumber = request.VatNumber,
            RequestedAt = DateTime.UtcNow
        };

        if (!ValidateVatFormat(request.VatNumber))
        {
            response.IsValid = false;
            response.Status = "Invalid";
            response.Reason = "Formato de NIF-IVA inválido";
            return response;
        }

        var countryCode = request.VatNumber.Substring(0, 2);
        var numberWithoutPrefix = request.VatNumber.Substring(2);

        var result = await _vies.ValidateAsync(countryCode, numberWithoutPrefix, cancellationToken);

        response.IsValid = result.IsValid;
        response.CompanyName = result.Name ?? string.Empty;
        response.Address = result.Address ?? string.Empty;
        response.Status = result.IsValid ? "Active" : "Invalid";
        response.Reason = result.ErrorMessage
            ?? (result.IsValid ? "Válido en el registro VIES" : "No encontrado en el registro VIES");

        var operation = new IntraEuOperation
        {
            CompanyId = request.CompanyId,
            Type = "Service",
            CountryCode = countryCode,
            PartnerVatId = request.VatNumber,
            ViesStatus = "NotReported",
        };

        _context.IntraEuOperations.Add(operation);
        await _context.SaveChangesAsync(cancellationToken);

        response.ValidationId = operation.Id;

        return response;
    }

    private static bool ValidateVatFormat(string vatNumber)
    {
        if (string.IsNullOrWhiteSpace(vatNumber) || vatNumber.Length < 4)
            return false;

        var countryCode = vatNumber.Substring(0, 2);
        var validCountryCodes = new[] { "ES", "IT", "FR", "DE", "NL", "BE", "AT", "PT", "GR" };
        
        return validCountryCodes.Contains(countryCode);
    }
}

public class ViesValidationResponse
{
    public Guid ValidationId { get; set; }
    public string VatNumber { get; set; } = string.Empty;
    public bool IsValid { get; set; }
    public string CompanyName { get; set; } = string.Empty;
    public string Address { get; set; } = string.Empty;
    public string Reason { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public DateTime RequestedAt { get; set; }
}
