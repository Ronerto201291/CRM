using Erp.Application.Common.Attributes;
using Erp.Application.DTOs;
using Erp.Modules.Crm.Application.Features.Crm.Commands;
using Erp.Modules.Crm.Application.Features.Crm.Queries;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Erp.Modules.Crm.Api.Controllers;

[ApiController, Route("api/[controller]"), Authorize, RequiredModule("CRM")]
public class LeadsController : ControllerBase
{
    private readonly IMediator _mediator;

    public LeadsController(IMediator mediator) => _mediator = mediator;

    [HttpGet]
    [RequirePermission(Permissions.Lead.Read)]
    public async Task<IActionResult> GetAll(
        [FromQuery] string? search,
        [FromQuery] string? status,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 50,
        CancellationToken ct = default)
    {
        var result = await _mediator.Send(new GetLeadsQuery
        {
            Search = search,
            Status = status,
            Page = page,
            PageSize = pageSize,
        }, ct);
        Response.Headers["X-Total-Count"] = result.TotalCount.ToString();
        return Ok(result);
    }

    [HttpGet("{id:guid}")]
    [RequirePermission(Permissions.Lead.Read)]
    public async Task<IActionResult> GetById(Guid id, CancellationToken ct)
    {
        var lead = await _mediator.Send(new GetLeadByIdQuery { Id = id }, ct);
        return lead == null ? NotFound() : Ok(lead);
    }

    [HttpPost]
    [RequirePermission(Permissions.Lead.Create)]
    public async Task<IActionResult> Create([FromBody] CreateLeadCommand cmd, CancellationToken ct)
    {
        var lead = await _mediator.Send(cmd, ct);
        return Created($"/api/leads/{lead.Id}", lead);
    }

    [HttpPut("{id:guid}")]
    [RequirePermission(Permissions.Lead.Update)]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateLeadCommand cmd, CancellationToken ct)
    {
        cmd.Id = id;
        var ok = await _mediator.Send(cmd, ct);
        return ok ? Ok(new { message = "Posible cliente actualizado" }) : NotFound();
    }

    [HttpDelete("{id:guid}")]
    [RequirePermission(Permissions.Lead.Delete)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        var ok = await _mediator.Send(new DeleteLeadCommand { Id = id }, ct);
        return ok ? NoContent() : NotFound();
    }

    [HttpPost("{id:guid}/convert-to-client")]
    [RequirePermission(Permissions.Lead.Convert)]
    public async Task<IActionResult> ConvertToClient(Guid id, CancellationToken ct)
    {
        try
        {
            var result = await _mediator.Send(new ConvertLeadToClientCommand { LeadId = id }, ct);
            return Ok(result);
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }
        catch (InvalidOperationException ex)
        {
            return Conflict(new { message = ex.Message });
        }
    }
}
