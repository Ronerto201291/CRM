using Erp.Application.Common.Attributes;
using Erp.Modules.Accounting.Application.Commands;
using Erp.Modules.Accounting.Application.Queries;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Erp.Modules.Accounting.Api.Controllers;

[ApiController]
[Route("api/v1/accounting/budgets")]
[Authorize]
[RequiredModule("Accounting")]
public class BudgetsController : ControllerBase
{
    private readonly IMediator _mediator;

    public BudgetsController(IMediator mediator) => _mediator = mediator;

    /// <summary>GET /api/v1/accounting/budgets?fiscalYear=2026</summary>
    [HttpGet]
    [RequirePermission(Permissions.Budget.Read)]
    public async Task<IActionResult> GetAll([FromQuery] int? fiscalYear, CancellationToken ct)
        => Ok(await _mediator.Send(new GetBudgetsQuery(fiscalYear), ct));

    /// <summary>GET /api/v1/accounting/budgets/{id}</summary>
    [HttpGet("{id:guid}")]
    [RequirePermission(Permissions.Budget.Read)]
    public async Task<IActionResult> GetById(Guid id, CancellationToken ct)
    {
        var budget = await _mediator.Send(new GetBudgetQuery(id), ct);
        return budget is null ? NotFound() : Ok(budget);
    }

    /// <summary>
    /// GET /api/v1/accounting/budgets/{id}/analysis
    /// Devuelve presupuesto vs real por línea, calculado desde los asientos contables reales.
    /// </summary>
    [HttpGet("{id:guid}/analysis")]
    [RequirePermission(Permissions.Budget.Read)]
    public async Task<IActionResult> GetAnalysis(Guid id, CancellationToken ct)
    {
        try
        {
            return Ok(await _mediator.Send(new GetBudgetAnalysisQuery(id), ct));
        }
        catch (KeyNotFoundException) { return NotFound(); }
    }

    /// <summary>POST /api/v1/accounting/budgets — Crear presupuesto en estado Draft</summary>
    [HttpPost]
    [RequirePermission(Permissions.Budget.Create)]
    public async Task<IActionResult> Create([FromBody] CreateBudgetRequest req, CancellationToken ct)
    {
        var id = await _mediator.Send(new CreateBudgetCommand(
            req.Name, req.FiscalYear, req.StartDate, req.EndDate), ct);

        return Created($"api/v1/accounting/budgets/{id}", new { id });
    }

    /// <summary>POST /api/v1/accounting/budgets/{id}/approve — Cambiar estado a Approved</summary>
    [HttpPost("{id:guid}/approve")]
    [RequirePermission(Permissions.Budget.Approve)]
    public async Task<IActionResult> Approve(Guid id, CancellationToken ct)
    {
        try
        {
            await _mediator.Send(new ApproveBudgetCommand(id), ct);
            return Ok(new { message = "Presupuesto aprobado." });
        }
        catch (KeyNotFoundException) { return NotFound(); }
        catch (InvalidOperationException ex) { return BadRequest(new { error = ex.Message }); }
    }

    /// <summary>POST /api/v1/accounting/budgets/{id}/close — Cerrar el presupuesto</summary>
    [HttpPost("{id:guid}/close")]
    [RequirePermission(Permissions.Budget.Manage)]
    public async Task<IActionResult> Close(Guid id, CancellationToken ct)
    {
        try
        {
            await _mediator.Send(new CloseBudgetCommand(id), ct);
            return Ok(new { message = "Presupuesto cerrado." });
        }
        catch (KeyNotFoundException) { return NotFound(); }
        catch (InvalidOperationException ex) { return BadRequest(new { error = ex.Message }); }
    }

    /// <summary>POST /api/v1/accounting/budgets/{id}/lines — Añadir línea al presupuesto</summary>
    [HttpPost("{id:guid}/lines")]
    [RequirePermission(Permissions.Budget.Manage)]
    public async Task<IActionResult> AddLine(Guid id, [FromBody] AddBudgetLineRequest req, CancellationToken ct)
    {
        try
        {
            var lineId = await _mediator.Send(new AddBudgetLineCommand(
                id, req.AccountId, req.AccountCode ?? string.Empty,
                req.CostCenterId, req.Type, req.BudgetedAmount), ct);
            return Created("", new { lineId });
        }
        catch (KeyNotFoundException) { return NotFound(); }
        catch (InvalidOperationException ex) { return BadRequest(new { error = ex.Message }); }
    }

    /// <summary>PUT /api/v1/accounting/budgets/lines/{lineId} — Actualizar importe línea</summary>
    [HttpPut("lines/{lineId:guid}")]
    [RequirePermission(Permissions.Budget.Manage)]
    public async Task<IActionResult> UpdateLine(Guid lineId, [FromBody] UpdateBudgetLineRequest req, CancellationToken ct)
    {
        try
        {
            await _mediator.Send(new UpdateBudgetLineCommand(lineId, req.BudgetedAmount), ct);
            return NoContent();
        }
        catch (KeyNotFoundException) { return NotFound(); }
    }

    /// <summary>DELETE /api/v1/accounting/budgets/lines/{lineId}</summary>
    [HttpDelete("lines/{lineId:guid}")]
    [RequirePermission(Permissions.Budget.Manage)]
    public async Task<IActionResult> DeleteLine(Guid lineId, CancellationToken ct)
    {
        try
        {
            await _mediator.Send(new DeleteBudgetLineCommand(lineId), ct);
            return NoContent();
        }
        catch (KeyNotFoundException) { return NotFound(); }
    }
}

public record CreateBudgetRequest(
    string Name,
    int FiscalYear,
    DateTime StartDate,
    DateTime EndDate
);

public record AddBudgetLineRequest(
    Guid? AccountId,
    string? AccountCode,
    Guid? CostCenterId,
    string Type,           // Revenue | Expense
    decimal BudgetedAmount
);

public record UpdateBudgetLineRequest(decimal BudgetedAmount);
