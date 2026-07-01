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

    public ReportsController(IMediator mediator)
    {
        _mediator = mediator;
    }

    /// <summary>
    /// Libro Diario: Registro cronol�gico de todas las operaciones.
    /// </summary>
    /// <param name="fechaInicio">Fecha inicio (YYYY-MM-DD)</param>
    /// <param name="fechaFin">Fecha fin (YYYY-MM-DD)</param>
    /// <param name="cuenta">Opcional: C�digo de cuenta para filtrar</param>
    [HttpGet("diario")]
    public async Task<ActionResult<GetDiarioResponse>> GetDiario(
        [FromQuery] DateTime fechaInicio,
        [FromQuery] DateTime fechaFin,
        [FromQuery] string? cuenta = null)
    {
        var query = new GetDiarioQuery
        {
            FechaInicio = fechaInicio,
            FechaFin = fechaFin,
            Cuenta = cuenta
        };

        var result = await _mediator.Send(query);
        return Ok(result);
    }

    /// <summary>
    /// Mayor: Resumen por cuenta de movimientos.
    /// </summary>
    /// <param name="fechaInicio">Fecha inicio (YYYY-MM-DD)</param>
    /// <param name="fechaFin">Fecha fin (YYYY-MM-DD)</param>
    /// <param name="cuentaFiltro">Opcional: C�digo de cuenta espec�fica</param>
    [HttpGet("mayor")]
    public async Task<ActionResult<GetMayorResponse>> GetMayor(
        [FromQuery] DateTime fechaInicio,
        [FromQuery] DateTime fechaFin,
        [FromQuery] string? cuentaFiltro = null)
    {
        var query = new GetMayorQuery
        {
            FechaInicio = fechaInicio,
            FechaFin = fechaFin,
            CuentaFiltro = cuentaFiltro
        };

        var result = await _mediator.Send(query);
        return Ok(result);
    }

    /// <summary>
    /// Balance Sheet: Estado de situaci�n financiera.
    /// </summary>
    /// <param name="fechaCorte">Fecha corte (YYYY-MM-DD)</param>
    [HttpGet("balance")]
    public async Task<ActionResult<GetBalanceSheetResponse>> GetBalance(
        [FromQuery] DateTime fechaCorte)
    {
        var query = new GetBalanceSheetQuery { FechaCorte = fechaCorte };
        var result = await _mediator.Send(query);
        return Ok(result);
    }

    /// <summary>
    /// Profit & Loss: Estado de resultados.
    /// </summary>
    /// <param name="fechaInicio">Fecha inicio (YYYY-MM-DD)</param>
    /// <param name="fechaFin">Fecha fin (YYYY-MM-DD)</param>
    [HttpGet("pyg")]
    public async Task<ActionResult<GetProfitAndLossResponse>> GetPyG(
        [FromQuery] DateTime fechaInicio,
        [FromQuery] DateTime fechaFin)
    {
        var query = new GetProfitAndLossQuery
        {
            FechaInicio = fechaInicio,
            FechaFin = fechaFin
        };

        var result = await _mediator.Send(query);
        return Ok(result);
    }
}
