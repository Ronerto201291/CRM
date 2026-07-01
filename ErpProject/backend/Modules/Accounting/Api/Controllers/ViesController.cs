using Microsoft.AspNetCore.Mvc;

namespace Erp.Modules.Accounting.Api.Controllers;

[ApiController]
[Route("api/v1/accounting/vies")]
public class ViesController : ControllerBase
{
    private static readonly Dictionary<string, (bool Valid, string Name, string Address)> ViesDatabase = new()
    {
        { "ES12345678Z", (true, "Test Company SL", "Calle Principal 123, Madrid") },
        { "ES87654321X", (true, "Demo Business Ltd", "Avenida Central 456, Barcelona") },
        { "IT12345678901", (true, "Societ� Italiana SPA", "Via Roma 789, Milano") },
        { "DE98765432101", (true, "Deutsche Firma GmbH", "Hauptstrasse 321, Berlin") },
    };

    [HttpPost("validate")]
    public IActionResult ValidateVat([FromBody] ValidateVatRequest request)
    {
        try
        {
            var isValid = ValidateVatFormat(request.VatNumber);
            var viesInfo = ViesDatabase.TryGetValue(request.VatNumber, out var info)
                ? info
                : (Valid: false, Name: "", Address: "");

            return Ok(new
            {
                validationId = Guid.NewGuid(),
                vatNumber = request.VatNumber,
                isValid = isValid && viesInfo.Valid,
                companyName = viesInfo.Name,
                address = viesInfo.Address,
                status = (isValid && viesInfo.Valid) ? "Valid" : "Invalid",
                requestedAt = DateTime.UtcNow
            });
        }
        catch (Exception ex)
        {
            return BadRequest(new { error = ex.Message });
        }
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

public class ValidateVatRequest
{
    public string VatNumber { get; set; } = string.Empty;
    public Guid CompanyId { get; set; }
}
