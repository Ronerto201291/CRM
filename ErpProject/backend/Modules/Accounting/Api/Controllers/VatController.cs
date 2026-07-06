using Erp.Application.Common.Attributes;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Erp.Modules.Accounting.Application.Features.Vat;

namespace Erp.Modules.Accounting.Api.Controllers;

[ApiController]
[Route("api/v1/accounting/vat")]
[Authorize]
[RequiredModule("Accounting")]
public class VatController : ControllerBase
{
    private readonly IMediator _mediator;

    public VatController(IMediator mediator) => _mediator = mediator;

    [HttpPost("calculate")]
    [RequirePermission(Permissions.Vat.Manage)]
    public async Task<IActionResult> CalculateVat([FromBody] CalculateVatCommand command, CancellationToken ct)
    {
        var result = await _mediator.Send(command, ct);
        return Ok(result);
    }

    [HttpGet("rates")]
    [RequirePermission(Permissions.Vat.Read)]
    public IActionResult GetVatRates() => Ok(SpanishVatRates.All);

    /// <summary>Nombre legacy «modelo330» — el modelo vigente es el 303 (el 330 quedó obsoleto en 2014).</summary>
    [HttpPost("declare/modelo330")]
    [RequirePermission(Permissions.Vat.Manage)]
    public async Task<IActionResult> DeclareModelo330([FromBody] DeclareModelo330Request request, CancellationToken ct)
    {
        try
        {
            var result = await _mediator.Send(new DeclareModelo330Command(request.Year, request.Quarter), ct);
            return Created("", result);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }
}

public class DeclareModelo330Request
{
    public int Year { get; set; }
    public int Quarter { get; set; } = 1;
}
