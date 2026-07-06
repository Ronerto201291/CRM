using Erp.Application.Common.Attributes;
using Erp.Modules.Crm.Application.Features.Alerts;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Erp.Modules.Crm.Api.Controllers;

[ApiController]
[Route("api/crm/alerts")]
[Authorize]
[RequiredModule("CRM")]
public class AlertsController : ControllerBase
{
    private readonly IMediator _mediator;

    public AlertsController(IMediator mediator) => _mediator = mediator;

    [HttpGet]
    [RequirePermission(Permissions.Alert.Read)]
    public async Task<IActionResult> GetAll(CancellationToken ct)
        => Ok(await _mediator.Send(new GetAlertsQuery(), ct));

    [HttpGet("pending")]
    [RequirePermission(Permissions.Alert.Read)]
    public async Task<IActionResult> GetPending(CancellationToken ct)
        => Ok(await _mediator.Send(new GetPendingAlertsQuery(), ct));

    [HttpPost]
    [RequirePermission(Permissions.Alert.Create)]
    public async Task<IActionResult> Create([FromBody] CreateAlertRequest req, CancellationToken ct)
    {
        try
        {
            var result = await _mediator.Send(new CreateAlertCommand(
                req.Title, req.Description, req.ScheduledAt, req.ClientId), ct);
            return Ok(result);
        }
        catch (ArgumentException ex) { return BadRequest(new { error = ex.Message }); }
    }

    [HttpPut("{id:guid}")]
    [RequirePermission(Permissions.Alert.Update)]
    public async Task<IActionResult> Update(Guid id, [FromBody] CreateAlertRequest req, CancellationToken ct)
    {
        try
        {
            return Ok(await _mediator.Send(new UpdateAlertCommand(
                id, req.Title, req.Description, req.ScheduledAt, req.ClientId), ct));
        }
        catch (KeyNotFoundException) { return NotFound(); }
    }

    [HttpPatch("{id:guid}/acknowledge")]
    [RequirePermission(Permissions.Alert.Manage)]
    public async Task<IActionResult> Acknowledge(Guid id, CancellationToken ct)
    {
        try
        {
            await _mediator.Send(new AcknowledgeAlertCommand(id), ct);
            return NoContent();
        }
        catch (KeyNotFoundException) { return NotFound(); }
    }

    [HttpPatch("{id:guid}/snooze")]
    [RequirePermission(Permissions.Alert.Manage)]
    public async Task<IActionResult> Snooze(Guid id, [FromBody] SnoozeRequest req, CancellationToken ct)
    {
        try
        {
            await _mediator.Send(new SnoozeAlertCommand(id, req.SnoozedUntil), ct);
            return NoContent();
        }
        catch (KeyNotFoundException) { return NotFound(); }
    }

    [HttpDelete("{id:guid}")]
    [RequirePermission(Permissions.Alert.Delete)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        try
        {
            await _mediator.Send(new DeleteAlertCommand(id), ct);
            return NoContent();
        }
        catch (KeyNotFoundException) { return NotFound(); }
    }
}

public record CreateAlertRequest(string Title, string? Description, DateTime ScheduledAt, Guid? ClientId);
public record SnoozeRequest(DateTime SnoozedUntil);
