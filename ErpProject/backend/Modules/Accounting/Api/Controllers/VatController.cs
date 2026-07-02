using MediatR;
using Microsoft.AspNetCore.Mvc;
using Erp.Modules.Accounting.Application.Features.Vat;

namespace Erp.Modules.Accounting.Api.Controllers;

[ApiController]
[Route("api/v1/accounting/vat")]
public class VatController : ControllerBase
{
    private readonly IMediator _mediator;

    public VatController(IMediator mediator) => _mediator = mediator;

    [HttpPost("calculate")]
    public async Task<IActionResult> CalculateVat([FromBody] CalculateVatCommand command, CancellationToken ct)
    {
        var result = await _mediator.Send(command, ct);
        return Ok(result);
    }

    [HttpGet("rates")]
    public IActionResult GetVatRates() => Ok(SpanishVatRates.All);

    [HttpPost("declare/modelo330")]
    public IActionResult DeclareModelo330([FromBody] object dto)
    {
        return Created("", new
        {
            id = Guid.NewGuid(),
            modelo = "330",
            status = "Declared",
            message = "Modelo 330 declarado exitosamente"
        });
    }
}

