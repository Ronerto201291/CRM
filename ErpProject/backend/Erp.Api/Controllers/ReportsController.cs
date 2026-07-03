using Erp.Modules.Accounting.Application.Queries;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Erp.Api.Controllers;

/// <summary>
/// Endpoint para reportes contables.
/// Todos los reportes incluyen datos del tenant actual (multi-tenant seguro).
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Authorize]
public class ReportsController : ControllerBase
{
    private readonly IMediator _mediator;

    public ReportsController(IMediator mediator) => _mediator = mediator;

    /// <summary>
    /// Libro Diario: Registro cronológico de todas las operaciones.
    /// </summary>
    [HttpGet("diario")]
    public async Task<ActionResult<GetDiarioResponse>> GetDiario([FromQuery] GetDiarioQuery query, CancellationToken ct)
        => Ok(await _mediator.Send(query, ct));

    /// <summary>
    /// Mayor: Resumen por cuenta de movimientos.
    /// </summary>
    [HttpGet("mayor")]
    public async Task<ActionResult<GetMayorResponse>> GetMayor([FromQuery] GetMayorQuery query, CancellationToken ct)
        => Ok(await _mediator.Send(query, ct));

    /// <summary>
    /// Balance Sheet: Estado de situación financiera.
    /// </summary>
    [HttpGet("balance")]
    public async Task<ActionResult<GetBalanceSheetResponse>> GetBalance([FromQuery] GetBalanceSheetQuery query, CancellationToken ct)
        => Ok(await _mediator.Send(query, ct));

    /// <summary>
    /// Profit & Loss: Estado de resultados.
    /// </summary>
    [HttpGet("pyg")]
    public async Task<ActionResult<GetProfitAndLossResponse>> GetPyG([FromQuery] GetProfitAndLossQuery query, CancellationToken ct)
        => Ok(await _mediator.Send(query, ct));
}
