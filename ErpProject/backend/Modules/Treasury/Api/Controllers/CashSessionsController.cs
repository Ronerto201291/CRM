using Erp.Modules.Treasury.Application.Features.CashSessions;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Erp.Modules.Treasury.Api.Controllers;

/// <summary>
/// Arqueo de caja (ADR-0018 #42b): apertura/cierre con esperado vs. contado.
/// </summary>
[ApiController]
[Route("api/treasury/cash-sessions")]
[Authorize]
public class CashSessionsController : ControllerBase
{
    private readonly IMediator _mediator;
    public CashSessionsController(IMediator mediator) => _mediator = mediator;

    [HttpGet]
    public async Task<IActionResult> GetAll(CancellationToken ct)
        => Ok(await _mediator.Send(new GetCashSessionsQuery(), ct));

    [HttpGet("open")]
    public async Task<IActionResult> GetOpen(CancellationToken ct)
        => Ok(await _mediator.Send(new GetOpenCashSessionQuery(), ct));

    [HttpPost("open")]
    public async Task<IActionResult> Open([FromBody] OpenCashSessionRequest body, CancellationToken ct)
        => Created("", await _mediator.Send(new OpenCashSessionCommand(body.OpeningBalance, body.Notes), ct));

    [HttpPost("{id}/close")]
    public async Task<IActionResult> Close(Guid id, [FromBody] CloseCashSessionRequest body, CancellationToken ct)
        => Ok(await _mediator.Send(new CloseCashSessionCommand(id, body.CountedClosingBalance, body.Notes), ct));
}

public record OpenCashSessionRequest(decimal OpeningBalance, string? Notes);
public record CloseCashSessionRequest(decimal CountedClosingBalance, string? Notes);
