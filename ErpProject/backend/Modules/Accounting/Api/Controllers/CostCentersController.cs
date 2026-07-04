using Erp.Application.Common.Attributes;
using Erp.Modules.Accounting.Application.Handlers;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Erp.Modules.Accounting.Api.Controllers;

[ApiController]
[Route("api/v1/accounting/cost-centers")]
[Authorize]
[RequiredModule("Accounting")]
public class CostCentersController : ControllerBase
{
    private readonly IMediator _mediator;

    public CostCentersController(IMediator mediator) => _mediator = mediator;

    [HttpGet]
    [RequirePermission(Permissions.CostCenter.Read)]
    public async Task<IActionResult> GetAll([FromQuery] bool? onlyActive, CancellationToken ct)
        => Ok(await _mediator.Send(new GetCostCentersQuery(onlyActive), ct));

    [HttpGet("{id:guid}")]
    [RequirePermission(Permissions.CostCenter.Read)]
    public async Task<IActionResult> GetById(Guid id, CancellationToken ct)
    {
        var cc = await _mediator.Send(new GetCostCenterQuery(id), ct);
        return cc is null ? NotFound() : Ok(cc);
    }

    [HttpPost]
    [RequirePermission(Permissions.CostCenter.Create)]
    public async Task<IActionResult> Create([FromBody] CreateCostCenterRequest req, CancellationToken ct)
    {
        try
        {
            var id = await _mediator.Send(new CreateCostCenterCommand(req.Code, req.Name, req.Type), ct);
            return Created($"api/v1/accounting/cost-centers/{id}", new { id });
        }
        catch (InvalidOperationException ex) { return Conflict(new { error = ex.Message }); }
    }

    [HttpPut("{id:guid}")]
    [RequirePermission(Permissions.CostCenter.Update)]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateCostCenterRequest req, CancellationToken ct)
    {
        try
        {
            await _mediator.Send(new UpdateCostCenterCommand(id, req.Name, req.Type, req.IsActive), ct);
            return NoContent();
        }
        catch (KeyNotFoundException) { return NotFound(); }
    }

    [HttpDelete("{id:guid}")]
    [RequirePermission(Permissions.CostCenter.Delete)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        try
        {
            await _mediator.Send(new DeleteCostCenterCommand(id), ct);
            return NoContent();
        }
        catch (KeyNotFoundException) { return NotFound(); }
        catch (InvalidOperationException ex) { return BadRequest(new { error = ex.Message }); }
    }
}

public record CreateCostCenterRequest(string Code, string Name, string Type);
public record UpdateCostCenterRequest(string Name, string Type, bool IsActive);
