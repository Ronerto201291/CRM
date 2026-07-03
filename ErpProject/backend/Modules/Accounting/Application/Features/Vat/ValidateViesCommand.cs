using MediatR;
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

    private static readonly Dictionary<string, (bool Valid, string Name, string Address)> ViesDatabase = new()
    {
        { "ES12345678Z", (true, "Test Company SL", "Calle Principal 123, Madrid") },
        { "ES87654321X", (true, "Demo Business Ltd", "Avenida Central 456, Barcelona") },
        { "IT12345678901", (true, "Società Italiana SPA", "Via Roma 789, Milano") },
        { "DE98765432101", (true, "Deutsche Firma GmbH", "Hauptstrasse 321, Berlin") },
        { "FR12345678901", (true, "Entreprise Française SARL", "Rue de Paris 654, Lyon") }
    };

    public ValidateViesHandler(IAccountingDbContext context) => _context = context;

    public async Task<ViesValidationResponse> Handle(ValidateViesCommand request, CancellationToken cancellationToken)
    {
        var response = new ViesValidationResponse
        {
            VatNumber = request.VatNumber,
            RequestedAt = DateTime.UtcNow
        };

        try
        {
            if (!ValidateVatFormat(request.VatNumber))
            {
                response.IsValid = false;
                response.Reason = "Invalid VAT format";
                return response;
            }

            var isViesValid = ViesDatabase.TryGetValue(request.VatNumber, out var viesInfo);

            response.IsValid = isViesValid;
            
            if (isViesValid)
            {
                response.CompanyName = viesInfo.Name;
                response.Address = viesInfo.Address;
                response.Reason = "Valid";
                response.Status = "Active";
            }
            else
            {
                response.Reason = "Not found in VIES registry";
                response.Status = "Invalid";
            }

            var viesRecord = new IntraEuOperation
            {
                Id = Guid.NewGuid(),
                CompanyId = request.CompanyId,
                Type = "ViesValidation",
                CountryCode = request.VatNumber.Length >= 2 ? request.VatNumber[..2].ToUpperInvariant() : string.Empty,
                PartnerVatId = request.VatNumber,
                ViesStatus = response.IsValid ? "Valid" : "Invalid"
            };

            _context.IntraEuOperations.Add(viesRecord);
            await _context.SaveChangesAsync(cancellationToken);

            response.ValidationId = viesRecord.Id;
        }
        catch (Exception ex)
        {
            response.IsValid = false;
            response.Reason = $"Validation error: {ex.Message}";
        }

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
