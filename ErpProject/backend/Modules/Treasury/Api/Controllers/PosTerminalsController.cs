using Erp.Application.Common.Attributes;
using Erp.Modules.Treasury.Application.Features.Pos;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Erp.Modules.Treasury.Api.Controllers;

[ApiController]
[Route("api/treasury/pos-terminals")]
[Authorize]
[RequiredModule("Treasury")]
[RequirePermission("Treasury", "Manage")]
public class PosTerminalsController(IMediator mediator) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> List(CancellationToken ct)
        => Ok(await mediator.Send(new GetPosTerminalsQuery(), ct));

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreatePosTerminalRequest body, CancellationToken ct)
    {
        var result = await mediator.Send(new CreatePosTerminalCommand(body.Name, body.TerminalCode), ct);
        return CreatedAtAction(nameof(List), new { id = result.Id }, result);
    }

    [HttpPost("{terminalId:guid}/payments")]
    public async Task<IActionResult> RegisterPayment(Guid terminalId, [FromBody] RegisterPosPaymentRequest body, CancellationToken ct)
    {
        var result = await mediator.Send(new RegisterPosPaymentCommand(
            terminalId, body.InvoiceId, body.Amount, body.ExternalReference), ct);
        return Ok(result);
    }
}

public record CreatePosTerminalRequest(string Name, string TerminalCode);
public record RegisterPosPaymentRequest(Guid InvoiceId, decimal? Amount, string? ExternalReference);
