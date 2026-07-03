using Erp.Modules.Accounting.Application.Features.Vat;
using MediatR;
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
    private readonly IMediator _mediator;

    public TaxController(IMediator mediator) => _mediator = mediator;

    [HttpPost("vies/validate")]
    [ProducesResponseType(typeof(ViesValidationResponse), 200)]
    [ProducesResponseType(typeof(object), 400)]
    public async Task<IActionResult> Validate([FromBody] ViesValidateRequest body, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(body.CountryCode) || body.CountryCode.Length != 2)
            return BadRequest(new { error = "countryCode debe ser un código ISO-2 de 2 letras, p.ej. 'FR'." });
        if (string.IsNullOrWhiteSpace(body.VatNumber))
            return BadRequest(new { error = "vatNumber no puede estar vacío." });

        try
        {
            var result = await _mediator.Send(new ValidateViesCommand
            {
                CountryCode = body.CountryCode,
                VatNumber = body.VatNumber
            }, ct);
            return Ok(new ViesValidationResponse(result));
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    [HttpGet("vies/validate")]
    [ProducesResponseType(typeof(ViesValidationResponse), 200)]
    public async Task<IActionResult> ValidateGet(
        [FromQuery] string countryCode, [FromQuery] string vatNumber, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(countryCode) || countryCode.Length != 2)
            return BadRequest(new { error = "countryCode debe ser un código ISO-2 de 2 letras." });
        if (string.IsNullOrWhiteSpace(vatNumber))
            return BadRequest(new { error = "vatNumber no puede estar vacío." });

        try
        {
            var result = await _mediator.Send(new ValidateViesCommand
            {
                CountryCode = countryCode,
                VatNumber = vatNumber
            }, ct);
            return Ok(new ViesValidationResponse(result));
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }
}

public sealed record ViesValidateRequest(string CountryCode, string VatNumber);

public sealed class ViesValidationResponse
{
    public Guid ValidationId { get; }
    public bool IsValid { get; }
    public string CountryCode { get; }
    public string VatNumber { get; }
    public string? Name { get; }
    public string? Address { get; }
    public string? RequestDate { get; }
    public string? ErrorMessage { get; }
    public string Advice { get; }

    public ViesValidationResponse(ValidateViesResult r)
    {
        ValidationId = r.ValidationId;
        IsValid = r.IsValid;
        CountryCode = r.CountryCode;
        VatNumber = r.VatNumber;
        Name = r.Name;
        Address = r.Address;
        RequestDate = r.RequestDate;
        ErrorMessage = r.ErrorMessage;
        Advice = r.Advice;
    }
}
