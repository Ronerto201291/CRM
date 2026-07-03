using Erp.Modules.Accounting.Application.Features.Recargo;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace Erp.Modules.Accounting.Api.Controllers;

[ApiController]
[Route("api/v1/accounting/recargo")]
public class RecargoController : ControllerBase
{
    private readonly IMediator _mediator;

    public RecargoController(IMediator mediator) => _mediator = mediator;

    /// <summary>
    /// Lista todos los recargos de equivalencia de la empresa para un periodo dado.
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> GetAll([FromQuery] int year, [FromQuery] int q, CancellationToken ct)
        => Ok(await _mediator.Send(new GetRecargosQuery(year, q), ct));

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateRecargoRequest request, CancellationToken ct)
    {
        var result = await _mediator.Send(new CreateRecargoCommand(
            request.SupplierVat,
            request.SupplierIsRE,
            request.BaseAmount,
            request.RechargeRate), ct);
        return Created("", result);
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById(Guid id, CancellationToken ct)
    {
        try
        {
            return Ok(await _mediator.Send(new GetRecargoByIdQuery(id), ct));
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { error = ex.Message });
        }
    }

    /// <summary>
    /// Genera la sección de recargo de equivalencia del Modelo 303 para un periodo.
    /// </summary>
    [HttpPost("{id:guid}/modelo303")]
    public async Task<IActionResult> GenerateModelo303(Guid id, CancellationToken ct)
    {
        try
        {
            return Ok(await _mediator.Send(new GenerateRecargoModelo303Command(id), ct));
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { error = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }
}

public class CreateRecargoRequest
{
    public string SupplierVat { get; set; } = string.Empty;
    public bool SupplierIsRE { get; set; }
    public decimal BaseAmount { get; set; }
    public decimal RechargeRate { get; set; } = 5.2m;
}
