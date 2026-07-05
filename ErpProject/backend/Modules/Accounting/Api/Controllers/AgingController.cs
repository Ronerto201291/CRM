using Erp.Modules.Accounting.Application.Features.Aging;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Erp.Modules.Accounting.Api.Controllers;

[ApiController]
[Route("api/v1/accounting/aging")]
[Authorize]
public class AgingController : ControllerBase
{
    private readonly IMediator _mediator;

    public AgingController(IMediator mediator) => _mediator = mediator;

    [HttpGet("receivables")]
    public async Task<IActionResult> GetReceivablesAging(CancellationToken ct)
    {
        var aging = await _mediator.Send(new GetReceivablesAgingQuery(), ct);
        return Ok(ToResponse(aging));
    }

    [HttpGet("payables")]
    public async Task<IActionResult> GetPayablesAging(CancellationToken ct)
    {
        var aging = await _mediator.Send(new GetPayablesAgingQuery(), ct);
        return Ok(ToResponse(aging));
    }

    [HttpGet("dso")]
    public async Task<IActionResult> GetDso(CancellationToken ct)
    {
        var dso = await _mediator.Send(new GetDsoQuery(), ct);
        return Ok(new { dso });
    }

    [HttpGet("dpo")]
    public async Task<IActionResult> GetDpo(CancellationToken ct)
    {
        var dpo = await _mediator.Send(new GetDpoQuery(), ct);
        return Ok(new { dpo });
    }

    [HttpPost("calculate")]
    public async Task<IActionResult> CalculateAging([FromBody] CalculateAgingRequest? dto, CancellationToken ct)
    {
        var result = await _mediator.Send(new CalculateAgingCommand(dto?.Type), ct);
        return Created(string.Empty, new { id = result.Id, status = result.Status, message = result.Message });
    }

    private static object ToResponse(Application.Interfaces.AgingBucketsDto aging) => new
    {
        type = aging.Type,
        totalAmount = aging.TotalAmount,
        current = aging.Current,
        days31To60 = aging.Days31To60,
        days61To90 = aging.Days61To90,
        days91Plus = aging.Days91Plus,
        dso = aging.Dso,
        dpo = aging.Dpo
    };
}

public class CalculateAgingRequest
{
    public string? Type { get; set; }
}
