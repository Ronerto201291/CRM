using Erp.Modules.Crm.Application.Features.Crm.Commands;
using Erp.Modules.Crm.Application.Features.Crm.Queries;
using Erp.Modules.Expenses.Application.Features.Expenses.Queries;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Erp.Modules.Crm.Api.Controllers;

[ApiController, Route("api/[controller]"), Authorize]
public class SuppliersController : ControllerBase
{
    private readonly IMediator _mediator;
    public SuppliersController(IMediator mediator) => _mediator = mediator;

    [HttpGet]
    public async Task<IActionResult> GetAll([FromQuery] string? search, CancellationToken ct)
        => Ok(await _mediator.Send(new GetSuppliersQuery { Search = search }, ct));

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById(Guid id, CancellationToken ct)
    {
        var result = await _mediator.Send(new GetSupplierByIdQuery { Id = id }, ct);
        if (result == null) return NotFound();

        result.Expenses = await _mediator.Send(new GetSupplierExpensesQuery { SupplierId = id }, ct);
        return Ok(result);
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateSupplierCommand cmd, CancellationToken ct)
    {
        var result = await _mediator.Send(cmd, ct);
        return Created("", result);
    }

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateSupplierCommand cmd, CancellationToken ct)
    {
        cmd.Id = id;
        try
        {
            var result = await _mediator.Send(cmd, ct);
            return result == null ? NotFound() : Ok(result);
        }
        catch (InvalidOperationException ex) { return BadRequest(new { error = ex.Message }); }
    }

    /// <summary>POST /api/suppliers/{id}/anonymize — RGPD Art. 17 derecho de supresión.</summary>
    [HttpPost("{id:guid}/anonymize")]
    public async Task<IActionResult> Anonymize(Guid id, CancellationToken ct)
    {
        var result = await _mediator.Send(new AnonymizeSupplierCommand { Id = id }, ct);
        return result ? Ok(new { message = "Datos personales anonimizados correctamente." }) : NotFound();
    }
}
