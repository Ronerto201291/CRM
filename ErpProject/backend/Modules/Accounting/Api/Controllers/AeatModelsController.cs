using Erp.Application.Common.Attributes;
using Erp.Modules.Accounting.Application.Features.Aeat;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Erp.Modules.Accounting.Api.Controllers;

[ApiController]
[Route("api/v1/accounting/aeat")]
[Authorize]
[RequiredModule("Accounting")]
public class AeatModelsController : ControllerBase
{
    private readonly IMediator _mediator;

    public AeatModelsController(IMediator mediator) => _mediator = mediator;

    [HttpGet("models")]
    [RequirePermission(Permissions.Vat.Read)]
    public async Task<IActionResult> ListModels(CancellationToken ct)
        => Ok(await _mediator.Send(new ListAeatModelsQuery(), ct));

    [HttpPost("modelo347")]
    [RequirePermission(Permissions.Vat.Manage)]
    public async Task<IActionResult> CreateModelo347([FromBody] CreateModelo347Request dto, CancellationToken ct)
    {
        var year = dto.Year > 0 ? dto.Year : DateTime.UtcNow.Year;
        var result = await _mediator.Send(new CreateModelo347Command(year), ct);
        return CreatedAtAction(nameof(GetModelo347), new { year = result.Year }, result);
    }

    [HttpGet("modelo347/{year:int}")]
    [RequirePermission(Permissions.Vat.Read)]
    public async Task<IActionResult> GetModelo347(int year, CancellationToken ct)
    {
        var result = await _mediator.Send(new GetModelo347Query(year), ct);
        return result is null ? NotFound() : Ok(new
        {
            year = result.Year,
            status = result.Status,
            totalRecords = result.TotalRecords,
            totalAmount = result.TotalAmount,
            id = result.Id,
            message = result.Message
        });
    }

    [HttpPost("modelo347/{id:guid}/export-txt")]
    [RequirePermission(Permissions.Accounting.Export)]
    public async Task<IActionResult> ExportModelo347(Guid id, CancellationToken ct)
    {
        var result = await _mediator.Send(new ExportModelo347TxtCommand(id), ct);
        return Ok(result);
    }

    [HttpPost("modelo111-190")]
    [RequirePermission(Permissions.Vat.Manage)]
    public async Task<IActionResult> CreateModelo111([FromBody] CreateModelo111Request dto, CancellationToken ct)
    {
        var year = dto.Year > 0 ? dto.Year : DateTime.UtcNow.Year;
        var month = dto.Month is >= 1 and <= 12 ? dto.Month : DateTime.UtcNow.Month;
        var result = await _mediator.Send(new CreateModelo111Command(year, month), ct);
        return Created(string.Empty, result);
    }

    [HttpPost("modelo200")]
    [RequirePermission(Permissions.Vat.Manage)]
    public async Task<IActionResult> CreateModelo200([FromBody] CreateModeloYearRequest dto, CancellationToken ct)
    {
        var year = dto.Year > 0 ? dto.Year : DateTime.UtcNow.Year;
        var result = await _mediator.Send(new CreateModelo200Command(year), ct);
        return Created(string.Empty, result);
    }

    [HttpPost("modelo202")]
    [RequirePermission(Permissions.Vat.Manage)]
    public async Task<IActionResult> CreateModelo202([FromBody] CreateModeloYearRequest dto, CancellationToken ct)
    {
        var year = dto.Year > 0 ? dto.Year : DateTime.UtcNow.Year;
        var result = await _mediator.Send(new CreateModelo202Command(year), ct);
        return Created(string.Empty, result);
    }

    [HttpPost("{id:guid}/sign-and-submit")]
    [RequirePermission(Permissions.Vat.Manage)]
    public async Task<IActionResult> SignAndSubmit(Guid id, CancellationToken ct)
    {
        var result = await _mediator.Send(new SignAndSubmitAeatModelCommand(id), ct);
        return Ok(result);
    }
}

public class CreateModelo347Request
{
    public int Year { get; set; }
}

public class CreateModelo111Request
{
    public int Year { get; set; }
    public int Month { get; set; }
}

public class CreateModeloYearRequest
{
    public int Year { get; set; }
}
