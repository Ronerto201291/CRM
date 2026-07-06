using Erp.Application.Common.Attributes;
using Erp.Modules.Accounting.Application.Commands;
using Erp.Modules.Accounting.Application.Features.Aging;
using Erp.Modules.Accounting.Application.Queries;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Erp.Modules.Accounting.Api.Controllers;

[ApiController, Route("api/accounting"), Authorize, RequiredModule("Accounting")]
public class AccountingController : ControllerBase
{
    private readonly IMediator _mediator;
    public AccountingController(IMediator mediator) => _mediator = mediator;

    [HttpGet("journal")]
    [RequirePermission(Permissions.Accounting.Read)]
    public async Task<IActionResult> GetJournal(
        [FromQuery] int? year,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 50,
        CancellationToken ct = default)
    {
        var result = await _mediator.Send(new GetJournalQuery { Year = year, Page = page, PageSize = pageSize }, ct);
        Response.Headers["X-Total-Count"] = result.TotalCount.ToString();
        return Ok(result);
    }

    [HttpGet("balance")]
    [RequirePermission(Permissions.Accounting.Read)]
    public async Task<IActionResult> GetBalance([FromQuery] int? year, CancellationToken ct)
        => Ok(await _mediator.Send(new GetTrialBalanceQuery { Year = year }, ct));

    [HttpGet("iva-soportado")]
    [RequirePermission(Permissions.Accounting.Read)]
    public async Task<IActionResult> GetIVASoportado([FromQuery] int? year, CancellationToken ct)
        => Ok(await _mediator.Send(new GetIVASoportadoQuery { Year = year }, ct));

    [HttpGet("iva-repercutido")]
    [RequirePermission(Permissions.Accounting.Read)]
    public async Task<IActionResult> GetIVARepercutido([FromQuery] int? year, CancellationToken ct)
        => Ok(await _mediator.Send(new GetIVARepercutidoQuery { Year = year }, ct));

    [HttpGet("liquidacion-iva")]
    [RequirePermission(Permissions.Accounting.Read)]
    public async Task<IActionResult> GetLiquidacionIVA([FromQuery] int? quarter, [FromQuery] int? year, CancellationToken ct)
        => Ok(await _mediator.Send(new GetLiquidacionIVAQuery { Quarter = quarter, Year = year }, ct));

    [HttpGet("pyg")]
    [RequirePermission(Permissions.Accounting.Read)]
    public async Task<IActionResult> GetPyG([FromQuery] int? year, CancellationToken ct)
        => Ok(await _mediator.Send(new GetPyGQuery { Year = year }, ct));

    /// <summary>GET /api/accounting/aging — antigüedad de cobros/pagos, DSO/DPO.</summary>
    [HttpGet("aging")]
    [RequirePermission(Permissions.Accounting.Read)]
    public async Task<IActionResult> GetAging([FromQuery] DateTime? asOf, CancellationToken ct)
        => Ok(await _mediator.Send(new GetAgingReportQuery(asOf), ct));

    [HttpGet("mayor/{accountCode}")]
    [RequirePermission(Permissions.Accounting.Read)]
    public async Task<IActionResult> GetMayor(string accountCode, [FromQuery] int? year, CancellationToken ct)
        => Ok(await _mediator.Send(new GetMayorControllerQuery { AccountCode = accountCode, Year = year }, ct));

    // ── Cierre Contable ─────────────────────────────────────────────────────

    /// <summary>
    /// GET /api/accounting/cierre — list all closed fiscal periods for this company.
    /// </summary>
    [HttpGet("cierre")]
    [RequirePermission(Permissions.Accounting.Read)]
    public async Task<IActionResult> GetCierres(CancellationToken ct)
        => Ok(await _mediator.Send(new GetFiscalPeriodsQuery(), ct));

    /// <summary>
    /// POST /api/accounting/cierre — execute the annual accounting close.
    /// Body: { "fiscalYear": 2025 }
    /// Generates PGC closing journal entries and locks the period.
    /// </summary>
    [HttpPost("cierre")]
    [RequirePermission(Permissions.Accounting.Close)]
    public async Task<IActionResult> CloseFiscalYear([FromBody] CloseFiscalYearRequest req, CancellationToken ct)
    {
        try
        {
            var result = await _mediator.Send(new CloseFiscalYearCommand(req.FiscalYear), ct);
            return Ok(result);
        }
        catch (InvalidOperationException ex)
        {
            return Conflict(new { error = ex.Message });
        }
    }
}

/// <summary>Request body for POST /api/accounting/cierre</summary>
public sealed record CloseFiscalYearRequest(int FiscalYear);
