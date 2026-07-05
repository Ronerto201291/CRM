using Erp.Application.Common.Attributes;
using Erp.Modules.Accounting.Application.Features.Vat;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Erp.Modules.Accounting.Api.Controllers;

[ApiController]
[Route("api/v1/accounting/vies")]
[Authorize]
[RequiredModule("Accounting")]
public class ViesController : ControllerBase
{
    private readonly IMediator _mediator;

    public ViesController(IMediator mediator) => _mediator = mediator;

    /// <summary>
    /// POST /api/v1/accounting/vies/validate
    /// Misma validaci├│n VIES real que TaxController, con registro en IntraEuOperations.
    /// </summary>
    [HttpPost("validate")]
    [RequirePermission(Permissions.Vies.Manage)]
    public async Task<IActionResult> ValidateVat([FromBody] ValidateViesRequest request, CancellationToken ct)
    {
        try
        {
            var result = await _mediator.Send(new ValidateViesCommand
            {
                CountryCode = request.CountryCode,
                VatNumber = request.VatNumber,
            }, ct);
            return Ok(result);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    /// <summary>
    /// GET /api/v1/accounting/vies/validate?countryCode=FR&amp;vatNumber=12345678901
    /// </summary>
    [HttpGet("validate")]
    [RequirePermission(Permissions.Vies.Read)]
    public async Task<IActionResult> ValidateGet(
        [FromQuery] string countryCode,
        [FromQuery] string vatNumber,
        CancellationToken ct)
    {
        try
        {
            var result = await _mediator.Send(new ValidateViesCommand
            {
                CountryCode = countryCode,
                VatNumber = vatNumber,
            }, ct);
            return Ok(result);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }
}

public class ValidateViesRequest
{
    public string CountryCode { get; set; } = string.Empty;
    public string VatNumber { get; set; } = string.Empty;
}
