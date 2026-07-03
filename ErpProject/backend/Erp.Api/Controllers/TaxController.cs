using Erp.Application.Common;
using Erp.Application.Common.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Erp.Api.Controllers;

/// <summary>
/// Tax compliance utilities: VIES validation, EU VAT lookup.
/// </summary>
[ApiController]
[Route("api/tax")]
[Authorize]
public class TaxController : ControllerBase
{
    private readonly IViesService _vies;

    public TaxController(IViesService vies)
    {
        _vies = vies;
    }

    /// <summary>
    /// Validates an EU VAT number via the official EU VIES service.
    /// Required before creating intracomunitario invoices (art. 25 LIVA).
    /// Obligation: verify buyer's VAT validity to justify IVA-exempt treatment.
    ///
    /// POST /api/tax/vies/validate
    /// Body: { "countryCode": "FR", "vatNumber": "12345678901" }
    ///
    /// Alternatively: GET /api/tax/vies/validate?countryCode=FR&amp;vatNumber=12345678901
    /// </summary>
    [HttpPost("vies/validate")]
    [ProducesResponseType(typeof(ViesValidationResponse), 200)]
    [ProducesResponseType(typeof(object), 400)]
    public async Task<IActionResult> Validate(
        [FromBody] ViesValidateRequest body, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(body.CountryCode) || body.CountryCode.Length != 2)
            return BadRequest(new { error = "countryCode debe ser un código ISO-2 de 2 letras, p.ej. 'FR'." });
        if (string.IsNullOrWhiteSpace(body.VatNumber))
            return BadRequest(new { error = "vatNumber no puede estar vacío." });

        var result = await _vies.ValidateAsync(body.CountryCode, body.VatNumber, ct);
        return Ok(new ViesValidationResponse(result));
    }

    /// <summary>
    /// GET /api/tax/vies/validate?countryCode=FR&amp;vatNumber=12345678901
    /// Convenient GET version (for quick lookups from frontend).
    /// </summary>
    [HttpGet("vies/validate")]
    [ProducesResponseType(typeof(ViesValidationResponse), 200)]
    public async Task<IActionResult> ValidateGet(
        [FromQuery] string countryCode, [FromQuery] string vatNumber, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(countryCode) || countryCode.Length != 2)
            return BadRequest(new { error = "countryCode debe ser un código ISO-2 de 2 letras." });
        if (string.IsNullOrWhiteSpace(vatNumber))
            return BadRequest(new { error = "vatNumber no puede estar vacío." });

        var result = await _vies.ValidateAsync(countryCode, vatNumber, ct);
        return Ok(new ViesValidationResponse(result));
    }
}

public sealed record ViesValidateRequest(string CountryCode, string VatNumber);

public sealed class ViesValidationResponse
{
    public bool    IsValid      { get; }
    public string  CountryCode  { get; }
    public string  VatNumber    { get; }
    public string? Name         { get; }
    public string? Address      { get; }
    public string? RequestDate  { get; }
    public string? ErrorMessage { get; }

    /// <summary>
    /// Advice for the user: whether they can issue an IVA-exempt intracomunitario invoice.
    /// </summary>
    public string Advice { get; }

    public ViesValidationResponse(ViesValidationResult r)
    {
        IsValid      = r.IsValid;
        CountryCode  = r.CountryCode;
        VatNumber    = r.VatNumber;
        Name         = r.Name;
        Address      = r.Address;
        RequestDate  = r.RequestDate;
        ErrorMessage = r.ErrorMessage;
        Advice = ViesResponseMapper.BuildAdvice(r);
    }
}
